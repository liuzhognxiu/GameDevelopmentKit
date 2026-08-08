using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Battle;
using UnityEngine;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class PveSelectionWidget : MonoBehaviour
    {
        private BuqiPveSelection m_Selection;
        private Action<BuqiPveDifficulty> m_Selected;

        public IReadOnlyList<BuqiPveChoiceCard> Cards
        {
            get
            {
                var cards = new List<BuqiPveChoiceCard>();
                if (m_Selection?.Cards == null)
                    return cards;

                foreach (BuqiPveChoiceCard card in m_Selection.Cards)
                    cards.Add(card?.Clone());
                return cards;
            }
        }

        public BuildSnapshot CurrentBoard =>
            BuqiRunBattleSnapshotUtility.CloneBuild(m_Selection?.CurrentBoard);

        public void Render(BuqiPveSelection selection, Action<BuqiPveDifficulty> selected)
        {
            Clear();
            if (selection == null || selection.Cards == null || selection.Cards.Count != 3)
                return;

            m_Selection = selection.Clone();
            m_Selected = selected;
            gameObject.SetActive(true);
        }

        public bool Select(BuqiPveDifficulty difficulty)
        {
            if (m_Selection?.Cards == null || m_Selected == null)
                return false;
            if (!Enum.IsDefined(typeof(BuqiPveDifficulty), difficulty))
                return false;
            if (!m_Selection.Cards.Exists(card => card != null && card.Difficulty == difficulty))
                return false;

            Action<BuqiPveDifficulty> selected = m_Selected;
            m_Selected = null;
            selected.Invoke(difficulty);
            return true;
        }

        public void Clear()
        {
            m_Selection = null;
            m_Selected = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            m_Selection = null;
            m_Selected = null;
        }
    }
}
