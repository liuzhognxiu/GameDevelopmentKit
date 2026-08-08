using System;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.DemoUI.Interaction
{
    public sealed class BuqiBazaarInteraction
    {
        private readonly BuqiRunEconomyService m_Economy;

        public BuqiBazaarInteraction(BuqiRunEconomyService economy)
        {
            m_Economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        public bool HasLock => false;

        public bool HasSellButton => false;

        public BuqiSellDragSession BeginSellDrag(BuqiRunEconomySnapshot source, string instanceId)
        {
            return new BuqiSellDragSession(m_Economy, m_Economy.QuoteBoardSale(source, instanceId));
        }
    }

    public sealed class BuqiSellDragSession
    {
        private readonly BuqiRunEconomyService m_Economy;
        private readonly BuqiRunSellQuote m_Quote;
        private bool m_Cancelled;
        private bool m_Completed;
        private bool m_OverSellZone;

        internal BuqiSellDragSession(BuqiRunEconomyService economy, BuqiRunSellQuote quote)
        {
            m_Economy = economy;
            m_Quote = quote;
        }

        public bool Accepted => m_Quote.Success && !m_Cancelled && !m_Completed;

        public int ExpectedRefund => m_Quote.ExpectedRefund;

        public bool PreviewVisible => Accepted && m_OverSellZone;

        public string FailureReason => m_Quote.FailureReason;

        public void SetOverSellZone(bool overSellZone)
        {
            m_OverSellZone = Accepted && overSellZone;
        }

        public void Cancel()
        {
            m_Cancelled = true;
            m_OverSellZone = false;
        }

        public BuqiRunEconomyResult Drop(BuqiRunEconomySnapshot current)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (!Accepted)
                return Reject(current, m_Cancelled ? "Sell drag was cancelled." : m_Quote.FailureReason);
            if (!m_OverSellZone)
                return Reject(current, "Item was not dropped over the sell zone.");

            m_Completed = true;
            m_OverSellZone = false;
            return m_Economy.SellQuoted(current, m_Quote);
        }

        private static BuqiRunEconomyResult Reject(BuqiRunEconomySnapshot source, string reason)
        {
            return new BuqiRunEconomyResult
            {
                Success = false,
                FailureReason = string.IsNullOrEmpty(reason) ? "Sell drag was rejected." : reason,
                Snapshot = source.Clone(),
            };
        }
    }
}
