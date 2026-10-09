using System.Collections.Generic;
using System.Linq;
using HumanError.Model.Content;
using UnityEngine;

namespace HumanError.Data
{
    /// <summary>One shift with its bulletin and difficulty levers (GDD §11 ShiftSO + BulletinSO).</summary>
    [CreateAssetMenu(menuName = "Human Error/Shift", fileName = "Shift")]
    public sealed class ShiftSO : ScriptableObject
    {
        public ShiftData data = new ShiftData();
    }
}
