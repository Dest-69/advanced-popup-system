namespace AdvancedPS.Core.System
{
    /// <summary>
    /// The non-generic surface of <see cref="AdvancedPS.Core.AdvancedPopup{TData}"/> — lets code that only holds an
    /// <see cref="IAdvancedPopup"/> (e.g. the spawn pool in <c>AdvancedPopupSystem.Despawn</c>) reset the data state
    /// without knowing the popup's data type.
    /// </summary>
    public interface IDataPopup
    {
        /// <summary> True while the popup holds bound data. </summary>
        bool HasData { get; }

        /// <summary> Forget the held data (the already-built UI is not touched). </summary>
        void ClearData();
    }
}
