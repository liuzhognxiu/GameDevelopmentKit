using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.Run.Economy;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiOperationInteractionTests
    {
        private const string Namespace = "Game.Hot.Buqi.DemoUI.Interaction.";

        [Test]
        public void OperationScreenRequiresThreeChoicesAndKeepsBoardVisible()
        {
            Type choiceType = RuntimeType("BuqiOperationChoice");
            Type modelType = RuntimeType("BuqiOperationInteractionModel");
            Array choices = Array.CreateInstance(choiceType, 3);
            choices.SetValue(CreateChoice(choiceType, "choice-a"), 0);
            choices.SetValue(CreateChoice(choiceType, "choice-b"), 1);
            choices.SetValue(CreateChoice(choiceType, "choice-c"), 2);

            object model = Activator.CreateInstance(
                modelType,
                new object[] { choices, new[] { "board-a", string.Empty, "board-b" } });

            Assert.That(GetProperty<bool>(model, "BoardVisible"), Is.True);
            Assert.That(AsObjects(GetProperty<object>(model, "Choices")).Length, Is.EqualTo(3));
            Assert.That(
                AsObjects(GetProperty<object>(model, "BoardInstanceIds")),
                Is.EqualTo(new[] { "board-a", string.Empty, "board-b" }));
        }

        [Test]
        public void OperationScreenRejectsAnyChoiceCountOtherThanThree()
        {
            Type choiceType = RuntimeType("BuqiOperationChoice");
            Type modelType = RuntimeType("BuqiOperationInteractionModel");
            Array choices = Array.CreateInstance(choiceType, 2);
            choices.SetValue(CreateChoice(choiceType, "choice-a"), 0);
            choices.SetValue(CreateChoice(choiceType, "choice-b"), 1);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                Activator.CreateInstance(modelType, new object[] { choices, Array.Empty<string>() }));

            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void SelectingOperationDoesNotHideOrMutateTheBoard()
        {
            Type choiceType = RuntimeType("BuqiOperationChoice");
            Type modelType = RuntimeType("BuqiOperationInteractionModel");
            Array choices = Array.CreateInstance(choiceType, 3);
            choices.SetValue(CreateChoice(choiceType, "choice-a"), 0);
            choices.SetValue(CreateChoice(choiceType, "choice-b"), 1);
            choices.SetValue(CreateChoice(choiceType, "choice-c"), 2);
            object model = Activator.CreateInstance(
                modelType,
                new object[] { choices, new[] { "board-a", "board-b" } });

            object accepted = modelType.GetMethod("Select").Invoke(model, new object[] { "choice-b" });
            object rejected = modelType.GetMethod("Select").Invoke(model, new object[] { "missing" });

            Assert.That(GetProperty<bool>(accepted, "Accepted"), Is.True);
            Assert.That(GetProperty<string>(accepted, "SelectedChoiceId"), Is.EqualTo("choice-b"));
            Assert.That(GetProperty<bool>(rejected, "Accepted"), Is.False);
            Assert.That(GetProperty<bool>(model, "BoardVisible"), Is.True);
            Assert.That(
                AsObjects(GetProperty<object>(model, "BoardInstanceIds")),
                Is.EqualTo(new[] { "board-a", "board-b" }));
        }

        [Test]
        public void ExposedChoicesCannotMutateOperationModel()
        {
            Type choiceType = RuntimeType("BuqiOperationChoice");
            Type modelType = RuntimeType("BuqiOperationInteractionModel");
            Array choices = Array.CreateInstance(choiceType, 3);
            choices.SetValue(CreateChoice(choiceType, "choice-a"), 0);
            choices.SetValue(CreateChoice(choiceType, "choice-b"), 1);
            choices.SetValue(CreateChoice(choiceType, "choice-c"), 2);
            object model = Activator.CreateInstance(
                modelType,
                new object[] { choices, new[] { "board-a" } });

            object exposedChoice = AsObjects(GetProperty<object>(model, "Choices"))[0];
            choiceType.GetField("Id").SetValue(exposedChoice, "tampered");

            object[] freshChoices = AsObjects(GetProperty<object>(model, "Choices"));
            object selection = modelType.GetMethod("Select").Invoke(model, new object[] { "choice-a" });
            Assert.That(choiceType.GetField("Id").GetValue(freshChoices[0]), Is.EqualTo("choice-a"));
            Assert.That(GetProperty<bool>(selection, "Accepted"), Is.True);
        }

        private static Type RuntimeType(string typeName)
        {
            Type type = typeof(BuqiRunEconomyService).Assembly.GetType(Namespace + typeName);
            Assert.That(type, Is.Not.Null, Namespace + typeName);
            return type;
        }

        private static object CreateChoice(Type choiceType, string id)
        {
            object choice = Activator.CreateInstance(choiceType);
            choiceType.GetField("Id").SetValue(choice, id);
            choiceType.GetField("Title").SetValue(choice, id);
            return choice;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static object[] AsObjects(object enumerable)
        {
            return ((IEnumerable)enumerable).Cast<object>().ToArray();
        }
    }
}
