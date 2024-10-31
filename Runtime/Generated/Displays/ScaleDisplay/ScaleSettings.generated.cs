using System;
using AdvancedPS.Core.System;
using UnityEngine;

namespace AdvancedPS.Core
{
    [Serializable]
    public class ScaleSettings : BaseSettings
    {
        /// <summary>
        /// Target scale of GameObject on shown.
        /// </summary>
        public Vector3 ShowScale { get; set; }
        /// <summary>
        /// Target scale of GameObject on hidden.
        /// </summary>
        public Vector3 HideScale { get; set; }
        
        /// <summary>
        /// Setting default values.
        /// </summary>
        public ScaleSettings()
        {
            HideScale = Vector3.zero;
            ShowScale = Vector3.one;
        }
    }
}