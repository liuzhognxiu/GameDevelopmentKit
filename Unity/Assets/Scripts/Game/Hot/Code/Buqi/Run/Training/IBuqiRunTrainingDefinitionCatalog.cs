using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Training
{
    public interface IBuqiRunTrainingDefinitionCatalog : IBuqiRunTrainingCatalog
    {
        IReadOnlyList<BuqiRunTrainingDefinition> TrainingDefinitions { get; }
    }
}
