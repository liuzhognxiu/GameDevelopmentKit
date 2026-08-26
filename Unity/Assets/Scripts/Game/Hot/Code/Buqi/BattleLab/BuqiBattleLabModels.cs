using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;

namespace Game.Hot.Buqi.BattleLab
{
    public enum BuqiBattleLabPhase { HeroSelection, Workbench }
    public enum BuqiBattleLabSide { Player, Enemy }
    public enum BuqiBattleLabOpponentMode { Preset, Custom }
    public enum BuqiBattleLabDragKind { Library, Board }

    public sealed class BuqiBattleLabHeroDefinition
    {
        public BuqiBattleLabHeroDefinition(
            string heroId,
            string displayName,
            string role,
            int initialExecution,
            int initialBuffer,
            int initialNoiseDebt)
        {
            HeroId = heroId;
            DisplayName = displayName;
            Role = role;
            InitialExecution = initialExecution;
            InitialBuffer = initialBuffer;
            InitialNoiseDebt = initialNoiseDebt;
        }

        public string HeroId { get; }
        public string DisplayName { get; }
        public string Role { get; }
        public int InitialExecution { get; }
        public int InitialBuffer { get; }
        public int InitialNoiseDebt { get; }
    }

    public sealed class BuqiBattleLabItemDefinition
    {
        public BuqiBattleLabItemDefinition(
            string definitionId,
            string displayName,
            string description,
            int size,
            BuqiQuality quality,
            int cooldownTicks,
            string archetypeId,
            string role,
            string positionHint,
            IReadOnlyList<string> tags,
            bool enabled,
            string error)
        {
            DefinitionId = definitionId;
            DisplayName = displayName;
            Description = description;
            Size = size;
            Quality = quality;
            CooldownTicks = cooldownTicks;
            ArchetypeId = archetypeId;
            Role = role;
            PositionHint = positionHint;
            var tagCopy = new string[tags == null ? 0 : tags.Count];
            for (int index = 0; index < tagCopy.Length; index++)
                tagCopy[index] = tags[index];
            Tags = Array.AsReadOnly(tagCopy);
            Enabled = enabled;
            Error = error;
        }

        public string DefinitionId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Size { get; }
        public BuqiQuality Quality { get; }
        public int CooldownTicks { get; }
        public string ArchetypeId { get; }
        public string Role { get; }
        public string PositionHint { get; }
        public IReadOnlyList<string> Tags { get; }
        public bool Enabled { get; }
        public string Error { get; }
    }

    public sealed class BuqiBattleLabPresetOpponent
    {
        private readonly BuildSnapshot m_Snapshot;

        public BuqiBattleLabPresetOpponent(
            string echoId,
            string displayName,
            string build,
            BuildSnapshot snapshot,
            IReadOnlyList<string> validationErrors)
        {
            EchoId = echoId;
            DisplayName = displayName;
            Build = build;
            m_Snapshot = CopySnapshot(snapshot);
            var validationErrorCopy = new string[
                validationErrors == null ? 0 : validationErrors.Count];
            for (int index = 0; index < validationErrorCopy.Length; index++)
                validationErrorCopy[index] = validationErrors[index];
            ValidationErrors = Array.AsReadOnly(validationErrorCopy);
        }

        public string EchoId { get; }
        public string DisplayName { get; }
        public string Build { get; }
        public BuildSnapshot Snapshot => CopySnapshot(m_Snapshot);
        public IReadOnlyList<string> ValidationErrors { get; }

        private static BuildSnapshot CopySnapshot(BuildSnapshot source)
        {
            if (source == null)
                return null;

            var copy = new BuildSnapshot
            {
                SnapshotId = source.SnapshotId,
                ContentVersion = source.ContentVersion,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };
            if (source.Items == null)
                return copy;

            foreach (ItemInstance sourceItem in source.Items)
            {
                if (sourceItem == null)
                {
                    copy.Items.Add(null);
                    continue;
                }

                var itemCopy = new ItemInstance
                {
                    InstanceId = sourceItem.InstanceId,
                    DefinitionId = sourceItem.DefinitionId,
                    Quality = sourceItem.Quality,
                    AnchorSlot = sourceItem.AnchorSlot,
                    AnnotationId = sourceItem.AnnotationId,
                };
                if (sourceItem.TemporaryModifiers != null)
                {
                    foreach (TemporaryModifier sourceModifier in sourceItem.TemporaryModifiers)
                    {
                        itemCopy.TemporaryModifiers.Add(sourceModifier == null
                            ? null
                            : new TemporaryModifier
                            {
                                Effect = sourceModifier.Effect,
                                SourceInstanceId = sourceModifier.SourceInstanceId,
                                RemainingTicks = sourceModifier.RemainingTicks,
                                Bps = sourceModifier.Bps,
                            });
                    }
                }
                copy.Items.Add(itemCopy);
            }
            return copy;
        }
    }

    public sealed class BuqiBattleLabPlacement
    {
        public BuqiBattleLabPlacement(
            string instanceId,
            string definitionId,
            string displayName,
            int size,
            BuqiQuality quality,
            int anchorSlot,
            string annotationId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            DisplayName = displayName;
            Size = size;
            Quality = quality;
            AnchorSlot = anchorSlot;
            AnnotationId = annotationId;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public int Size { get; }
        public BuqiQuality Quality { get; }
        public int AnchorSlot { get; }
        public string AnnotationId { get; }
    }

    public sealed class BuqiBattleLabBoardView
    {
        public BuqiBattleLabBoardView(
            int slotCount,
            IReadOnlyList<BuqiBattleLabPlacement> placements,
            IReadOnlyList<string> occupiedInstanceIds)
        {
            SlotCount = slotCount;
            var placementCopy = new BuqiBattleLabPlacement[
                placements == null ? 0 : placements.Count];
            for (int index = 0; index < placementCopy.Length; index++)
                placementCopy[index] = placements[index];
            Placements = Array.AsReadOnly(placementCopy);
            var occupiedInstanceIdCopy = new string[
                occupiedInstanceIds == null ? 0 : occupiedInstanceIds.Count];
            for (int index = 0; index < occupiedInstanceIdCopy.Length; index++)
                occupiedInstanceIdCopy[index] = occupiedInstanceIds[index];
            OccupiedInstanceIds = Array.AsReadOnly(occupiedInstanceIdCopy);
        }

        public int SlotCount { get; }
        public IReadOnlyList<BuqiBattleLabPlacement> Placements { get; }
        public IReadOnlyList<string> OccupiedInstanceIds { get; }
    }

    public sealed class BuqiBattleLabView
    {
        public BuqiBattleLabView(
            BuqiBattleLabPhase phase,
            BuqiBattleLabHeroDefinition playerHero,
            BuqiBattleLabOpponentMode opponentMode,
            string selectedPresetId,
            BuqiBattleLabHeroDefinition customEnemyHero,
            BuqiBattleLabBoardView playerBoard,
            BuqiBattleLabBoardView customEnemyBoard,
            int simulationCount)
        {
            Phase = phase;
            PlayerHero = playerHero;
            OpponentMode = opponentMode;
            SelectedPresetId = selectedPresetId;
            CustomEnemyHero = customEnemyHero;
            PlayerBoard = playerBoard;
            CustomEnemyBoard = customEnemyBoard;
            SimulationCount = simulationCount;
        }

        public BuqiBattleLabPhase Phase { get; }
        public BuqiBattleLabHeroDefinition PlayerHero { get; }
        public BuqiBattleLabOpponentMode OpponentMode { get; }
        public string SelectedPresetId { get; }
        public BuqiBattleLabHeroDefinition CustomEnemyHero { get; }
        public BuqiBattleLabBoardView PlayerBoard { get; }
        public BuqiBattleLabBoardView CustomEnemyBoard { get; }
        public int SimulationCount { get; }
    }

    public sealed class BuqiBattleLabPlacementPreview
    {
        public BuqiBattleLabPlacementPreview(
            BuqiBattleLabSide side,
            int anchorSlot,
            int span,
            IReadOnlyList<int> coveredSlots,
            bool accepted,
            string reason)
        {
            Side = side;
            AnchorSlot = anchorSlot;
            Span = span;
            var coveredSlotCopy = new int[coveredSlots == null ? 0 : coveredSlots.Count];
            for (int index = 0; index < coveredSlotCopy.Length; index++)
                coveredSlotCopy[index] = coveredSlots[index];
            CoveredSlots = Array.AsReadOnly(coveredSlotCopy);
            Accepted = accepted;
            Reason = reason;
        }

        public BuqiBattleLabSide Side { get; }
        public int AnchorSlot { get; }
        public int Span { get; }
        public IReadOnlyList<int> CoveredSlots { get; }
        public bool Accepted { get; }
        public string Reason { get; }
    }

    public sealed class BuqiBattleLabCommandResult
    {
        public BuqiBattleLabCommandResult(
            bool accepted,
            string reason,
            BuqiBattleLabView view)
        {
            Accepted = accepted;
            Reason = reason;
            View = view;
        }

        public bool Accepted { get; }
        public string Reason { get; }
        public BuqiBattleLabView View { get; }
    }
}
