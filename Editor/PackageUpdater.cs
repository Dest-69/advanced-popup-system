using System;
using System.IO;
using System.Text.RegularExpressions;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Everything about the shape of this install: which version is out there, and how to move between the two shapes
    /// APS can take.
    ///
    /// Editing layers regenerates <c>PopupLayerEnum</c> <i>inside</i> the package, so it needs a writable copy — a
    /// Git/registry install must be <b>embedded</b> first. That carries two costs this class pays back:
    /// <list type="bullet">
    /// <item>Package Manager labels an embedded package <b>Custom</b> and updates neither it nor (in practice) a Git
    /// install — hence <see cref="BeginUpdate"/>, and <see cref="EnsureLatestChecked"/> to know an update exists;</item>
    /// <item>the copy is stuck in the project until <see cref="BeginDetach"/> hands it back to Package Manager;</item>
    /// <item>the Customization lock is a <see cref="PlayerPrefs"/> flag that must not outlive the install it was taken
    /// against — <see cref="BeginEmbed"/>/<see cref="EmbedInProgress"/> let the Layers panel tell "still embedding"
    /// from "the embedded copy is gone".</item>
    /// </list>
    ///
    /// <b>The dangerous move is unavoidable, so it is fenced instead.</b> UPM refuses to add a package that is
    /// currently embedded — <i>"is already embedded and cannot be updated, it must first be manually removed from the
    /// `Packages` folder"</i> — so the folder has to go first, and for the length of the request the project has no APS
    /// at all: a failure there would take this class, the rest of the run, and the consumer's compile down together.
    /// <see cref="ReplaceEmbeddedCopy"/> fences it with a move-aside backup and a held assembly-reload lock; the details
    /// are on that method, and changing the order there is how this breaks.
    ///
    /// Every UPM request recompiles, so the step is parked in <see cref="SessionState"/> and picked up by
    /// <see cref="Resume"/> on the next load. Session lifetime is deliberate: a run that did not finish before the
    /// editor closed is abandoned, never replayed against a project state it no longer knows.
    /// </summary>
    [InitializeOnLoad]
    internal static class PackageUpdater
    {
        #region VARIABLES

        private const string ManifestPath = "Packages/manifest.json";

        private const string StepKey = "APS_PkgUpdate_Step";
        private const string IntentKey = "APS_PkgUpdate_Intent";
        private const string UrlKey = "APS_PkgUpdate_Url";
        private const string NameKey = "APS_PkgUpdate_Name";
        private const string FromVersionKey = "APS_PkgUpdate_FromVersion";
        private const string RetryKey = "APS_PkgUpdate_Retry";
        private const string EmbedPendingKey = "APS_PkgUpdate_EmbedPending";
        private const string LatestKey = "APS_PkgUpdate_Latest";
        private const string LatestUrlKey = "APS_PkgUpdate_LatestUrl";

        /// <summary>Where a run stands between domain reloads — a reload drops the in-flight <see cref="Request"/>.</summary>
        private enum Step
        {
            None = 0,
            /// <summary>The embedded copy is parked in <c>Library/</c> and the Git copy is being installed in its place.</summary>
            Replacing = 1,
            /// <summary>Plain Add over a non-embedded install — nothing had to move out of the way.</summary>
            Reinstalling = 2,
            /// <summary>Make the fresh copy writable again so layer editing keeps working.</summary>
            Embedding = 3
        }

        /// <summary>What the run is for — it decides where the run stops and what the user is told.</summary>
        private enum Intent
        {
            /// <summary>Read-only Git install → newest revision, still read-only. No folder to replace.</summary>
            UpdateGit = 0,
            /// <summary>Embedded copy → newest revision, embedded again.</summary>
            UpdateEmbedded = 1,
            /// <summary>Embedded copy → hand it back to Package Manager as a read-only Git install.</summary>
            Detach = 2,
            /// <summary>Registry install → <c>name@version</c>. Mechanically the same plain Add as <see cref="UpdateGit"/>, kept apart so the messages can tell the truth about where the copy came from.</summary>
            UpdateRegistry = 3
        }

        /// <summary>Where the embedded copy is parked while its replacement installs. Under <c>Library/</c>: never scanned by UPM or the AssetDatabase, same volume as <c>Packages/</c> so the move is a rename.</summary>
        private const string BackupRoot = "Library/APS_EmbedBackup";

        /// <summary>Seconds before a stalled UPM request gives up — it runs with assembly reload locked, so it can't be left hanging.</summary>
        private const double RequestTimeout = 180d;

        private static Request _request;
        private static double _deadline;
        private static bool _reloadLocked;
        private static UnityWebRequest _versionRequest;

        private static PackageInfo _pkg;
        private static bool _pkgResolved;
        private static string _gitUrl;
        private static bool _gitUrlResolved;

        static PackageUpdater()
        {
            // Runs on every domain reload; Resume no-ops unless a run is parked.
            EditorApplication.delayCall += Resume;
        }

        #endregion

        #region Install shape

        /// <summary>True when APS is an embedded copy under <c>Packages/</c> — what Package Manager labels "Custom".</summary>
        public static bool IsEmbedded => Package?.source == PackageSource.Embedded;

        /// <summary>
        /// How this install gets to a newer version. Deliberately has no "it can't" member: the window offers
        /// <b>Update</b> whenever <see cref="UpdateAvailable"/>, and that check answers for every shape APS can be
        /// installed in — so every shape owes the button an action (see <see cref="BeginUpdate"/>).
        /// </summary>
        public enum UpdateRoute
        {
            /// <summary>APS reinstalls itself from its Git URL — embedded or Git, the two shapes Package Manager won't update.</summary>
            Reinstall = 0,
            /// <summary>A registry install: APS adds <c>name@version</c> itself, so the button updates rather than delegating.</summary>
            Registry = 1,
            /// <summary>A local path/tarball, a loose folder under <c>Assets/</c>, anything unrecognised — say what this shape needs and open the download.</summary>
            Manual = 2
        }

        /// <summary>
        /// Which route this install takes. <see cref="UpdateRoute.Reinstall"/> needs a Git URL to re-add; without one
        /// even an embedded copy is a hand-made folder nobody but its owner should replace, so it falls through to
        /// <see cref="UpdateRoute.Manual"/>.
        /// </summary>
        public static UpdateRoute Route
        {
            get
            {
                PackageSource? source = Package?.source;
                if ((source == PackageSource.Embedded || source == PackageSource.Git) && GitUrl != null)
                    return UpdateRoute.Reinstall;

                return source == PackageSource.Registry ? UpdateRoute.Registry : UpdateRoute.Manual;
            }
        }

        /// <summary>What the <b>Update</b> button is about to do — the button is offered for every shape, so it has to say which one it got.</summary>
        public static string UpdateTooltip
        {
            get
            {
                switch (Route)
                {
                    case UpdateRoute.Reinstall:
                        return "Reinstall APS at the newest revision of the Git URL it came from. Unity recompiles a few times.";
                    case UpdateRoute.Registry:
                        return "Install the newest APS from the package registry it came from. Unity recompiles.";
                    default:
                        return "APS cannot replace this copy on its own — this explains what this install needs and opens the Releases page.";
                }
            }
        }

        /// <summary>True while a run is in flight (it spans several domain reloads).</summary>
        public static bool IsBusy => CurrentStep != Step.None;

        /// <summary>
        /// A writable copy is being made right now. The panel must not read the package's momentary read-only state as
        /// "the Customization lock is stale" while this is true.
        /// </summary>
        public static bool EmbedInProgress => IsBusy || SessionState.GetBool(EmbedPendingKey, false);

        /// <summary>Called by the panel once the package reads writable again — the embed landed. Per-repaint, so it no-ops when already clear.</summary>
        public static void ClearEmbedPending()
        {
            if (SessionState.GetBool(EmbedPendingKey, false))
                SessionState.SetBool(EmbedPendingKey, false);
        }

        /// <summary>
        /// Embeds the package so the layer enum can be regenerated in place, remembering that we asked, and reconciles
        /// once the writable copy lands. The poll is required: the caller is usually the layer heal, and Unity does not
        /// reload the domain while a compile error stands, so nothing else would notice the package turned writable.
        /// A reload landing mid-request kills this closure — <see cref="ClearEmbedPending"/> and
        /// <c>LayerEnumSyncPostprocessor.SyncOnLoad</c> cover that.
        /// </summary>
        public static void BeginEmbed()
        {
            SessionState.SetBool(EmbedPendingKey, true);

            EmbedRequest request = FileSearcher.EmbedPackage();
            if (request == null)
            {
                // Loose folder under Assets/ — writable already.
                SessionState.SetBool(EmbedPendingKey, false);
                return;
            }

            double deadline = EditorApplication.timeSinceStartup + RequestTimeout;

            void PollEmbed()
            {
                if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;

                EditorApplication.update -= PollEmbed;
                SessionState.SetBool(EmbedPendingKey, false);

                if (!request.IsCompleted)
                {
                    APLogger.LogError($"[APS] Embedding a writable copy of the package did not answer within " +
                                      $"{RequestTimeout:0} seconds. Your layers are safe in ProjectSettings/APS_Layers.json " +
                                      "— turn Customization off and on again in APS ▸ Layers to retry.");
                    return;
                }

                if (request.Status != StatusCode.Success)
                {
                    APLogger.LogError("[APS] Could not embed a writable copy of the package: " +
                                      (request.Error?.message ?? "the Package Manager request failed.") +
                                      " Your layers are safe in ProjectSettings/APS_Layers.json.");
                    return;
                }

                InvalidatePackage();
                FileSearcher.InvalidatePackage();
                LayerCatalog.Reconcile();
            }

            EditorApplication.update += PollEmbed;
        }

        /// <summary>
        /// The user handed the embedded copy back this session (<see cref="BeginDetach"/>). The layer heal reads this so
        /// it does not immediately embed again to restore layers — undoing a deliberate action is worse than the
        /// fallback the detach dialog already warned about.
        /// </summary>
        public static bool DetachedThisSession => SessionState.GetBool(DetachedKey, false);

        private const string DetachedKey = "APS_PkgDetachedThisSession";

        #endregion

        #region Latest version

        /// <summary>The newest version published on the Git remote, or null while unknown (not checked, offline, non-GitHub host).</summary>
        public static string LatestVersion
        {
            get
            {
                string value = SessionState.GetString(LatestKey, string.Empty);
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        /// <summary>True when the remote is ahead of what is installed.</summary>
        public static bool UpdateAvailable => IsNewer(LatestVersion, InstalledVersion);

        /// <summary>
        /// The version the window shows. Goes through <see cref="PackageVersionHelper"/> rather than
        /// <see cref="PackageInfo"/> so the badge also works for a loose folder under <c>Assets/</c>, where there is no
        /// package to ask; its "Dev" fallback simply fails the version parse, which reads as "no update".
        /// </summary>
        private static string InstalledVersion => PackageVersionHelper.GetVersion();

        /// <summary>True while a remote check is in flight — the window shows a "sync" badge for it.</summary>
        public static bool IsCheckingLatest => _versionRequest != null;

        /// <summary>
        /// Reads <c>package.json</c> off the Git remote. Deliberately reads the raw file at the ref the install actually
        /// tracks — a pinned branch/tag, else the default branch — so the answer is "what an update would give me", not
        /// "what the newest tag is". Silent on failure: a missing badge is the right amount of noise for a nice-to-have,
        /// and the editor must never stall or spam on a network hiccup.
        /// </summary>
        /// <param name="force">
        /// Ask again even if this ref was already checked. The APS window passes true when the user <b>opens</b> it — one
        /// small GET for a deliberate action — and false from <c>OnEnable</c>, which also fires on every domain reload and
        /// would otherwise hit the remote on each recompile.
        /// </param>
        public static void EnsureLatestChecked(Action onDone = null, bool force = false)
        {
            if (_versionRequest != null) return;

            string rawUrl = RawPackageJsonUrl(GitUrl);
            if (rawUrl == null) return;

            // "Already checked" is scoped to what was checked, not to the session: nothing to ask (no Git URL yet) must
            // not count as an answer, and an install that changes ref — detach, a re-pin — deserves a fresh look.
            // Re-evaluating is nearly free, since GitUrl is cached per domain.
            if (!force && SessionState.GetString(LatestUrlKey, string.Empty) == rawUrl) return;
            SessionState.SetString(LatestUrlKey, rawUrl);

            _versionRequest = UnityWebRequest.Get(rawUrl);
            _versionRequest.timeout = 10;
            _versionRequest.SendWebRequest();

            // Polled from the editor loop rather than AsyncOperation.completed, which is unreliable outside play mode.
            void PollVersion()
            {
                if (_versionRequest == null) { EditorApplication.update -= PollVersion; return; }
                if (!_versionRequest.isDone) return;

                EditorApplication.update -= PollVersion;
                if (_versionRequest.result == UnityWebRequest.Result.Success)
                {
                    string version = ParseJsonFields(_versionRequest.downloadHandler.text)?.version;
                    if (!string.IsNullOrEmpty(version))
                        SessionState.SetString(LatestKey, version);
                }
                _versionRequest.Dispose();
                _versionRequest = null;
                onDone?.Invoke();
            }

            EditorApplication.update += PollVersion;
        }

        /// <summary>
        /// Git URL → the raw <c>package.json</c> behind it. GitHub only (the host APS ships from); anything else returns
        /// null and the version badge simply stays hidden. <c>HEAD</c> resolves to the default branch, so no guessing
        /// between <c>main</c> and <c>master</c>; an explicit <c>#ref</c> pin wins, since that is what the install tracks.
        /// </summary>
        private static string RawPackageJsonUrl(string gitUrl)
        {
            if (string.IsNullOrEmpty(gitUrl)) return null;

            Match match = Regex.Match(gitUrl, @"github\.com[:/]([^/]+)/([^/#?]+?)(?:\.git)?([?#].*)?$");
            if (!match.Success) return null;

            // UPM's two modifiers are independent and can combine: "?path=<folder>" packages a subfolder of the repo,
            // "#<ref>" pins a branch/tag/commit. Reading the root package.json of a "?path=" repo would answer with a
            // completely different package's version — a confidently wrong badge is worse than none.
            string tail = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
            int hash = tail.IndexOf('#');

            string reference = hash >= 0 && hash + 1 < tail.Length ? tail.Substring(hash + 1) : "HEAD";
            string query = hash >= 0 ? tail.Substring(0, hash) : tail;

            Match subfolder = Regex.Match(query, @"[?&]path=([^&]+)");
            string subPath = subfolder.Success ? "/" + subfolder.Groups[1].Value.Trim('/') : string.Empty;

            return $"https://raw.githubusercontent.com/{match.Groups[1].Value}/{match.Groups[2].Value}/{reference}{subPath}/package.json";
        }

        /// <summary>Numeric <c>x.y.z</c> comparison; anything unparseable answers "no update" rather than a false alarm.</summary>
        private static bool IsNewer(string candidate, string current)
        {
            int[] a = ParseVersion(candidate);
            int[] b = ParseVersion(current);
            if (a == null || b == null) return false;

            for (int i = 0; i < 3; i++)
            {
                if (a[i] != b[i]) return a[i] > b[i];
            }
            return false;
        }

        private static int[] ParseVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;

            Match match = Regex.Match(version.Trim(), @"^v?(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success) return null;

            return new[]
            {
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0
            };
        }

        #endregion

        #region Entry points

        /// <summary>
        /// Moves this install to the newest version by whichever <see cref="Route"/> its shape allows. The dispatch
        /// exists because the version check speaks about <b>every</b> install shape: a badge that says an update exists
        /// next to no button at all is a dead end, so the button is never the thing that goes missing.
        /// </summary>
        public static void BeginUpdate()
        {
            if (IsBusy) return;

            switch (Route)
            {
                case UpdateRoute.Reinstall: BeginReinstall(); return;
                case UpdateRoute.Registry: BeginRegistryUpdate(); return;
                default: ExplainManualUpdate(); return;
            }
        }

        /// <summary>
        /// Reinstalls APS at the newest revision of the Git URL it came from — embedding it again afterwards if that is
        /// how it was installed, so layer editing keeps working. User-confirmed. Consumer state (layers, settings,
        /// canvases, custom displays) lives outside the package and survives; the layer enum is healed from
        /// <c>ProjectSettings/APS_Layers.json</c> by <see cref="LayerEnumSyncPostprocessor"/>.
        /// </summary>
        private static void BeginReinstall()
        {
            PackageInfo pkg = Package;
            string url = GitUrl;
            bool embedded = pkg.source == PackageSource.Embedded;

            string what = embedded
                ? $"The embedded copy at Packages/{pkg.name} is replaced, then embedded again so layers stay editable. " +
                  "Your layers, settings, canvases and custom displays live outside the package and are kept — but any " +
                  "hand-edit inside the package folder is lost."
                : "Package Manager cannot update a Git dependency in place, so APS re-adds it — the read-only install " +
                  "is replaced by the newest revision.";

            if (!EditorUtility.DisplayDialog("Update Advanced Popup System",
                    $"Reinstall {pkg.name} (currently {pkg.version}) from:\n\n{url}\n\n{what}\n\n" +
                    "Unity will recompile a few times.",
                    "Update", "Cancel"))
                return;

            Start(embedded ? Intent.UpdateEmbedded : Intent.UpdateGit, pkg, url);
        }

        /// <summary>
        /// Registry install: a plain <c>Add</c> of <c>name@version</c>, run through the same state machine as the rest
        /// so it survives the recompiles — the button updates here rather than opening Package Manager, since a second
        /// button in another window is not an update. The version asked for is the one the badge shows (read off
        /// GitHub): a registry that has not published it answers with UPM's own error, which <see cref="Fail"/> passes
        /// on — better than silently installing whatever that registry considers newest.
        /// </summary>
        private static void BeginRegistryUpdate()
        {
            PackageInfo pkg = Package;
            string version = LatestVersion;
            if (pkg == null || string.IsNullOrEmpty(version))
            {
                ExplainManualUpdate();
                return;
            }

            if (!EditorUtility.DisplayDialog("Update Advanced Popup System",
                    $"Update {pkg.name} {pkg.version} → {version} from its package registry.\n\n" +
                    "Your layers, settings, canvases and custom displays live outside the package and are kept.\n\n" +
                    "Unity will recompile.",
                    "Update", "Cancel"))
                return;

            Start(Intent.UpdateRegistry, pkg, pkg.name + "@" + version);
        }

        /// <summary>
        /// Nothing here can safely replace this copy: a <c>file:</c> path or a tarball belongs to whoever pointed UPM at
        /// it, and a loose folder under <c>Assets/</c> would have to be deleted and re-imported — the user's call, not a
        /// button's. So the button does the one useful thing left: names what this shape needs, and opens the download.
        /// </summary>
        private static void ExplainManualUpdate()
        {
            string version = LatestVersion != null ? "Advanced Popup System v" + LatestVersion : "A newer Advanced Popup System";
            string releases = ReleasesUrl;

            PackageInfo pkg = Package;
            string how = pkg == null
                ? "This copy sits in your Assets folder, so it updates the way it got there: delete the " +
                  "advanced-popup-system folder and import the new package over it. Your layers, settings, canvases and " +
                  "generated displays live outside that folder and are kept."
                : $"This copy is installed as {SourceLabel(pkg.source)}, which APS cannot replace on its own — update it " +
                  "where it lives, or reinstall APS from its Git URL (Package Manager ▸ Install package from git URL), " +
                  "which keeps the Update button working from then on.";

            if (releases == null)
            {
                EditorUtility.DisplayDialog("Update Advanced Popup System", $"{version} is out.\n\n{how}", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Update Advanced Popup System", $"{version} is out.\n\n{how}",
                    "Open Releases", "Cancel"))
                Application.OpenURL(releases);
        }

        /// <summary>Install shapes named the way whoever made them would recognise — for the manual-update dialog.</summary>
        private static string SourceLabel(PackageSource source)
        {
            switch (source)
            {
                case PackageSource.Local: return "a local folder (a \"file:\" entry in Packages/manifest.json)";
                case PackageSource.LocalTarball: return "a local .tgz tarball";
                case PackageSource.Registry: return "a package registry";
                case PackageSource.Embedded: return "a copy in your Packages/ folder with no Git URL to restore it from";
                case PackageSource.Git: return "a Git dependency whose URL could not be read back";
                default: return "a package shape APS does not recognise";
            }
        }

        /// <summary>
        /// Hands the embedded copy back to Package Manager as a plain read-only Git install — the way out of "Custom".
        /// User-confirmed; no-op unless <see cref="IsEmbedded"/>. The layer <b>list</b> survives in
        /// <c>ProjectSettings/APS_Layers.json</c>, but a read-only package can't be regenerated, so the compiled enum
        /// falls back to the layers the package ships with until it is embedded again.
        /// </summary>
        public static void BeginDetach()
        {
            if (IsBusy || !IsEmbedded) return;

            PackageInfo pkg = Package;
            string url = GitUrl;
            if (url == null)
            {
                EditorUtility.DisplayDialog("Advanced Popup System",
                    "Could not work out which Git URL this copy came from, so it cannot be handed back automatically.\n\n" +
                    "Delete Packages/" + pkg.name + "/ by hand and install APS from its Git URL via " +
                    "Package Manager ▸ Install package from git URL.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove embedded copy",
                    $"Packages/{pkg.name} is replaced by a read-only install from:\n\n{url}\n\n" +
                    "Layer editing turns off. Your layer list is safe in ProjectSettings/APS_Layers.json, but a " +
                    "read-only package cannot regenerate the enum, so PopupLayerEnum falls back to the layers APS " +
                    "ships with — popups tagged with a custom layer keep their value but lose its name until you " +
                    "enable Customization again.\n\nAny hand-edit inside the package folder is lost.",
                    "Remove & reinstall", "Cancel"))
                return;

            // Recorded before the run starts: the layer heal must not "fix" this by embedding again (see DetachedThisSession).
            SessionState.SetBool(DetachedKey, true);
            Start(Intent.Detach, pkg, url);
        }

        private static void Start(Intent intent, PackageInfo pkg, string url)
        {
            SessionState.SetInt(IntentKey, (int)intent);
            SessionState.SetString(UrlKey, url);
            SessionState.SetString(NameKey, pkg.name);
            SessionState.SetString(FromVersionKey, pkg.version ?? "?");
            SessionState.SetInt(RetryKey, 0);

            // Nothing in the way — a plain re-add moves a read-only install to the newest revision. Only an embedded
            // copy has to be got out of UPM's way first.
            if (intent == Intent.UpdateGit || intent == Intent.UpdateRegistry)
                Send(Step.Reinstalling, Client.Add(url));
            else
                ReplaceEmbeddedCopy(pkg, url);
        }

        /// <summary>
        /// Swaps the embedded copy for the Git one. UPM <b>refuses</b> to add a package that is currently embedded
        /// ("…is already embedded and cannot be updated, it must first be manually removed from the `Packages` folder"),
        /// so the folder has to go first — which is the dangerous order: for the length of the request the project has
        /// no APS, and if the Add fails, the code that would put it back is gone with it.
        ///
        /// Two guards make it safe. The copy is <b>moved, not deleted</b> — a rename into <see cref="BackupRoot"/>, so
        /// undoing it is a rename back. And <b>assembly reload stays locked</b> for the whole request, so this class
        /// cannot be unloaded mid-flight; the lock is released only once the Add has reported success (or the backup is
        /// back in place). <see cref="RequestTimeout"/> bounds it, because a lock that never lifts freezes the editor.
        /// </summary>
        private static void ReplaceEmbeddedCopy(PackageInfo pkg, string url)
        {
            string root = pkg.resolvedPath?.Replace('\\', '/').TrimEnd('/');
            string relative = "Packages/" + pkg.name;

            // Never hand a move anything but <…>/Packages/<package name>.
            if (string.IsNullOrEmpty(root) || (root != relative && !root.EndsWith("/" + relative, StringComparison.Ordinal)))
            {
                Fail($"refusing to move an unexpected path: {root}");
                return;
            }

            string backup = BackupRoot + "/" + pkg.name;
            try
            {
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
                Directory.CreateDirectory(BackupRoot);
                Directory.Move(root, backup);
            }
            catch (Exception ex)
            {
                Fail($"could not move {root} aside: {ex.Message}");
                return;
            }

            // The path goes in the log before anything can go wrong with it — if the editor dies mid-run, this line is
            // how the copy gets found again.
            APLogger.Log($"<color=green>[APS]</color> Embedded copy parked at {backup} while {url} installs. " +
                         "If this run is interrupted, move that folder back to Packages/ to restore it.");

            LockReload();
            Send(Step.Replacing, Client.Add(url));
        }

        private static void LockReload()
        {
            if (_reloadLocked) return;
            _reloadLocked = true;
            EditorApplication.LockReloadAssemblies();
        }

        private static void UnlockReload()
        {
            if (!_reloadLocked) return;
            _reloadLocked = false;
            EditorApplication.UnlockReloadAssemblies();
        }

        /// <summary>Puts the parked copy back where it was. Best effort — the caller is already on a failure path.</summary>
        private static void RestoreBackup(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return;

            string backup = BackupRoot + "/" + packageName;
            string root = "Packages/" + packageName;
            try
            {
                if (Directory.Exists(backup) && !Directory.Exists(root))
                    Directory.Move(backup, root);
            }
            catch (Exception ex)
            {
                APLogger.LogError($"[APS] Could not restore the embedded copy from {backup}: {ex.Message}. " +
                                  "Move that folder back to Packages/ by hand.");
            }
        }

        private static void DiscardBackup(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return;
            try
            {
                string backup = BackupRoot + "/" + packageName;
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            catch { /* a stale folder under Library/ is harmless; it is not worth failing a finished run over */ }
        }

        #endregion

        #region Run

        private static Step CurrentStep
        {
            get => (Step)SessionState.GetInt(StepKey, 0);
            set => SessionState.SetInt(StepKey, (int)value);
        }

        private static Intent CurrentIntent => (Intent)SessionState.GetInt(IntentKey, 0);

        private static void Send(Step step, Request request)
        {
            CurrentStep = step;
            _request = request;
            _deadline = EditorApplication.timeSinceStartup + RequestTimeout;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        /// <summary>In-domain completion: the request object outlived the step, so its status is authoritative.</summary>
        private static void Poll()
        {
            if (_request == null) return;

            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < _deadline) return;
                Fail($"the Package Manager request did not answer within {RequestTimeout:0} seconds.");
                return;
            }

            Request done = _request;
            _request = null;
            EditorApplication.update -= Poll;

            if (done.Status != StatusCode.Success)
            {
                Fail(done.Error?.message ?? "the Package Manager request failed.");
                return;
            }

            InvalidatePackage();
            switch (CurrentStep)
            {
                case Step.Replacing:
                    // The Git copy is in, so the parked one is dead weight — and the lock can lift.
                    DiscardBackup(SessionState.GetString(NameKey, null));
                    UnlockReload();
                    AfterReinstall();
                    break;
                case Step.Reinstalling: AfterReinstall(); break;
                case Step.Embedding: Finish(); break;
            }
        }

        /// <summary>
        /// Post-reload completion: the request object is gone, so the project state is the only evidence. A reload can
        /// also land between a request being sent and the package catching up, which looks exactly like a failure —
        /// hence the single bounded retry.
        /// </summary>
        private static void Resume()
        {
            Step step = CurrentStep;
            if (step == Step.None) return;

            InvalidatePackage();
            PackageInfo pkg = Package;
            string name = SessionState.GetString(NameKey, null);
            string url = SessionState.GetString(UrlKey, null);

            switch (step)
            {
                // Reached only if something unlocked the domain behind our back — the replace step holds the reload
                // lock precisely so it doesn't happen. Judge it by whether the Git copy actually landed.
                case Step.Replacing:
                    if (pkg != null && pkg.source != PackageSource.Embedded)
                    {
                        DiscardBackup(name);
                        UnlockReload();
                        AfterReinstall();
                        return;
                    }
                    RestoreBackup(name);
                    UnlockReload();
                    Fail("the reinstall was interrupted; the embedded copy was put back. Nothing was lost — try again.");
                    return;

                case Step.Reinstalling:
                    if (pkg == null)
                    {
                        if (TakeRetry() && !string.IsNullOrEmpty(url)) Send(Step.Reinstalling, Client.Add(url));
                        else
                            Fail($"the reinstalled copy did not resolve. Packages/manifest.json still asks for {url} — " +
                                 "reopen the project (or Package Manager ▸ Refresh) once you are online.");
                        return;
                    }
                    AfterReinstall();
                    return;

                case Step.Embedding:
                    if (pkg != null && pkg.source == PackageSource.Embedded) Finish();
                    else if (TakeRetry()) StartEmbed();
                    else Fail("the package was reinstalled from Git but embedding it did not complete — enable " +
                              "Customization again to retry.");
                    return;
            }
        }

        /// <summary>The fresh Git copy is in — only an update of an embedded copy goes on to make it writable again.</summary>
        private static void AfterReinstall()
        {
            if (CurrentIntent == Intent.UpdateEmbedded) StartEmbed();
            else Finish();
        }

        private static void StartEmbed()
        {
            string name = SessionState.GetString(NameKey, null);
            if (string.IsNullOrEmpty(name))
            {
                Fail("the run lost track of what it was updating.");
                return;
            }

            SessionState.SetBool(EmbedPendingKey, true);
            Send(Step.Embedding, Client.Embed(name));
        }

        /// <summary>One retry for the whole run, so a genuinely stuck run can never loop.</summary>
        private static bool TakeRetry()
        {
            if (SessionState.GetInt(RetryKey, 0) > 0) return false;
            SessionState.SetInt(RetryKey, 1);
            return true;
        }

        private static void Finish()
        {
            InvalidatePackage();
            FileSearcher.InvalidatePackage();

            // The fresh copy carries the enum APS ships with, so the layers have to be written back — that is what makes
            // the "restored" line below true. Not after a Detach: there the shipped default is the intended end state.
            if (FileSearcher.IsPackageWritable) LayerCatalog.Reconcile();

            Intent intent = CurrentIntent;
            string from = SessionState.GetString(FromVersionKey, "?");
            string to = Package?.version ?? "?";
            ClearState();

            string message = intent == Intent.Detach
                ? $"Advanced Popup System is a read-only Package Manager install again (v{to}).\n\nLayer editing is " +
                  "off — enable Customization in APS ▸ Layers to embed it again. Your layer list is still in " +
                  "ProjectSettings/APS_Layers.json."
                : (from == to ? $"Reinstalled {to} — that was already the newest version." : $"Updated {from} → {to}.") +
                  (intent == Intent.UpdateEmbedded
                      ? "\n\nThe copy is embedded again, so layer editing still works, and your layers were restored " +
                        "from ProjectSettings/APS_Layers.json."
                      : string.Empty);

            APLogger.Log($"<color=green>[APS]</color> {message.Replace("\n\n", " ")}");
            EditorUtility.DisplayDialog("Advanced Popup System", message, "OK");
        }

        private static void Fail(string reason)
        {
            // Failing mid-swap means the project is sitting there without APS — put the parked copy back before
            // anything else. Idempotent, so the paths that already restored it can still route through here.
            if (CurrentStep == Step.Replacing)
                RestoreBackup(SessionState.GetString(NameKey, null));

            ClearState();
            APLogger.LogError($"[APS] Package update stopped: {reason}");
            EditorUtility.DisplayDialog("Advanced Popup System", "Package update stopped: " + reason, "OK");
        }

        private static void ClearState()
        {
            UnlockReload();
            _request = null;
            EditorApplication.update -= Poll;
            CurrentStep = Step.None;
            SessionState.SetBool(EmbedPendingKey, false);
            SessionState.EraseInt(IntentKey);
            SessionState.EraseInt(RetryKey);
            SessionState.EraseString(UrlKey);
            SessionState.EraseString(NameKey);
            SessionState.EraseString(FromVersionKey);
        }

        #endregion

        #region Package facts

        /// <summary>The APS package, or null when APS is a loose folder under <c>Assets/</c> (the dev project).</summary>
        private static PackageInfo Package
        {
            get
            {
                if (_pkgResolved) return _pkg;
                _pkgResolved = true;
                try { _pkg = PackageInfo.FindForAssembly(typeof(PackageUpdater).Assembly); }
                catch { _pkg = null; }
                return _pkg;
            }
        }

        /// <summary>
        /// The Git URL this copy came from, or null when there is none to find. Prefers the consumer's manifest entry —
        /// embedding does not rewrite it, and it is the only source that preserves a pinned branch or tag
        /// (<c>….git#v2.2.0</c>). Falls back to the repository the package declares in its own <c>package.json</c>
        /// (default branch), which is also the only source a loose folder under <c>Assets/</c> has — it can't be
        /// updated from here, but it can still be told a newer version exists. Cached: the UI reads this every repaint.
        /// </summary>
        private static string GitUrl
        {
            get
            {
                if (_gitUrlResolved) return _gitUrl;
                _gitUrl = null;

                PackageInfo pkg = Package;

                // A miss is cached only when there was a package to ask. Asking before the package layer is up would
                // otherwise pin "no Git URL" for the whole domain — and with it the update route — while the badge,
                // which lives in SessionState and outlives any single domain, still says an update exists.
                _gitUrlResolved = pkg != null;

                if (pkg != null)
                {
                    string fromManifest = ReadManifestDependency(pkg.name);
                    if (IsGitUrl(fromManifest)) return _gitUrl = fromManifest;
                }

                string declared = ReadDeclaredRepository(pkg?.resolvedPath ?? FileSearcher.PackageRootPath);
                if (!IsGitUrl(declared)) return null;

                _gitUrlResolved = true;
                return _gitUrl = declared.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? declared : declared + ".git";
            }
        }

        /// <summary>
        /// The repository's Releases page — where a manual update comes from. Derived from <see cref="GitUrl"/>, which
        /// is non-null whenever the version check answered at all (it reads raw <c>package.json</c> off that same URL),
        /// so a shape that got a badge always gets a link to go with it.
        /// </summary>
        private static string ReleasesUrl
        {
            get
            {
                Match match = Regex.Match(GitUrl ?? string.Empty, @"github\.com[:/]([^/]+)/([^/#?]+?)(?:\.git)?([?#].*)?$");
                return match.Success
                    ? $"https://github.com/{match.Groups[1].Value}/{match.Groups[2].Value}/releases"
                    : null;
            }
        }

        private static void InvalidatePackage()
        {
            _pkgResolved = false;
            _gitUrlResolved = false;
        }

        /// <summary>A registry version ("2.2.2") and a local path ("file:../…") both fail this, which is the point.</summary>
        private static bool IsGitUrl(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.StartsWith("http://", StringComparison.Ordinal)
                || value.StartsWith("https://", StringComparison.Ordinal)
                || value.StartsWith("ssh://", StringComparison.Ordinal)
                || value.StartsWith("git@", StringComparison.Ordinal)
                || value.StartsWith("git+", StringComparison.Ordinal);
        }

        private static string ReadManifestDependency(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return null;
            try
            {
                if (!File.Exists(ManifestPath)) return null;
                Match match = Regex.Match(File.ReadAllText(ManifestPath),
                    "\"" + Regex.Escape(packageName) + "\"\\s*:\\s*\"([^\"]*)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch { return null; }
        }

        private static string ReadDeclaredRepository(string packageRootFs)
        {
            if (string.IsNullOrEmpty(packageRootFs)) return null;
            try
            {
                string manifest = Path.Combine(packageRootFs, "package.json");
                return File.Exists(manifest) ? ParseJsonFields(File.ReadAllText(manifest))?.url : null;
            }
            catch { return null; }
        }

        private static PackageJsonFields ParseJsonFields(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PackageJsonFields>(json); }
            catch { return null; }
        }

        /// <summary>The two <c>package.json</c> fields this class reads — of the local copy and of the remote one.</summary>
        [Serializable]
        private class PackageJsonFields
        {
            public string url;
            public string version;
        }

        #endregion
    }
}
