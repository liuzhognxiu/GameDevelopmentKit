using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI.Widgets;
using System;
using UnityEngine;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class OperationChoiceWidget : BuqiStageWidgetBase
    {
        [SerializeField] private BuqiRouteNodeCardWidget[] m_RouteCards = Array.Empty<BuqiRouteNodeCardWidget>();

        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.OperationChoice;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            if (m_RouteCards.Length > 0)
                return;
            foreach (BuqiDemoChoiceView choice in view.Choices)
                AddAction(choice.Title, BuqiUIDemoCommandType.SelectOperation, choice.Id);
        }

        protected override void CompleteRender(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            for (int index = 0; index < m_RouteCards.Length; index++)
            {
                BuqiRouteNodeCardWidget card = m_RouteCards[index];
                bool visible = card != null && index < view.RouteNodes.Count;
                if (card != null) card.gameObject.SetActive(visible);
                if (!visible) continue;
                card.Render(view.RouteNodes[index], id => submit?.Invoke(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectRouteNode,
                    PrimaryId = id,
                }));
            }
        }

        protected override void OnCleared()
        {
            foreach (BuqiRouteNodeCardWidget card in m_RouteCards)
                card?.Clear();
        }
    }
}
