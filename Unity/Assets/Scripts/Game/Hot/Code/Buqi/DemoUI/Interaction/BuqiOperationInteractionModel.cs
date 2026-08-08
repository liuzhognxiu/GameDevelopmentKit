using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Hot.Buqi.DemoUI.Interaction
{
    public sealed class BuqiOperationChoice
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Description = string.Empty;
        public int Cost;

        internal BuqiOperationChoice Clone()
        {
            return (BuqiOperationChoice)MemberwiseClone();
        }
    }

    public sealed class BuqiOperationSelectionResult
    {
        public bool Accepted { get; internal set; }
        public string FailureReason { get; internal set; } = string.Empty;
        public string SelectedChoiceId { get; internal set; } = string.Empty;
    }

    public sealed class BuqiOperationInteractionModel
    {
        public const int RequiredChoiceCount = 3;

        private readonly ReadOnlyCollection<BuqiOperationChoice> m_Choices;
        private readonly ReadOnlyCollection<string> m_BoardInstanceIds;

        public BuqiOperationInteractionModel(
            IEnumerable<BuqiOperationChoice> choices,
            IEnumerable<string> boardInstanceIds)
        {
            if (choices == null)
                throw new ArgumentNullException(nameof(choices));
            if (boardInstanceIds == null)
                throw new ArgumentNullException(nameof(boardInstanceIds));

            var copiedChoices = new List<BuqiOperationChoice>();
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiOperationChoice choice in choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.Id))
                    throw new ArgumentException("Operation choice id is required.", nameof(choices));
                if (!choiceIds.Add(choice.Id))
                    throw new ArgumentException("Operation choice ids must be unique.", nameof(choices));
                copiedChoices.Add(choice.Clone());
            }

            if (copiedChoices.Count != RequiredChoiceCount)
                throw new ArgumentException("Operation screen requires exactly three choices.", nameof(choices));

            var copiedBoard = new List<string>();
            foreach (string instanceId in boardInstanceIds)
                copiedBoard.Add(instanceId ?? string.Empty);

            m_Choices = copiedChoices.AsReadOnly();
            m_BoardInstanceIds = copiedBoard.AsReadOnly();
        }

        public bool BoardVisible => true;

        public IReadOnlyList<BuqiOperationChoice> Choices
        {
            get
            {
                var copies = new List<BuqiOperationChoice>(m_Choices.Count);
                for (int index = 0; index < m_Choices.Count; index++)
                    copies.Add(m_Choices[index].Clone());
                return copies.AsReadOnly();
            }
        }

        public IReadOnlyList<string> BoardInstanceIds => m_BoardInstanceIds;

        public BuqiOperationSelectionResult Select(string choiceId)
        {
            for (int index = 0; index < m_Choices.Count; index++)
            {
                if (string.Equals(m_Choices[index].Id, choiceId, StringComparison.Ordinal))
                {
                    return new BuqiOperationSelectionResult
                    {
                        Accepted = true,
                        SelectedChoiceId = choiceId,
                    };
                }
            }

            return new BuqiOperationSelectionResult
            {
                Accepted = false,
                FailureReason = "Operation choice was not found.",
            };
        }
    }
}
