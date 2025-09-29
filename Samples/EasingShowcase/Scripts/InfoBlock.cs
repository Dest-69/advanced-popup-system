using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core.Examples
{
    public class InfoBlock : MonoBehaviour
    {
        [SerializeField] private Text text;

        public void SetText(string t)
        {
            text.text = t;
        }
    }
}