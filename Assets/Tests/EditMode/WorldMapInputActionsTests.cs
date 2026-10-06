using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorldMapInputActionsTests
    {
        [Test]
        public void WorldMap_HasACommandAction_BoundToTheRightMouseButton()
        {
            Assert.IsNotNull(InputSystem.actions, "No project-wide input actions asset.");

            InputActionMap map = InputSystem.actions.FindActionMap("WorldMap");
            Assert.IsNotNull(map, "WorldMap action map");

            InputAction command = map.FindAction("Command");
            Assert.IsNotNull(command, "Command action");
            Assert.AreEqual(InputActionType.Button, command.type);
            Assert.AreEqual(1, command.bindings.Count);
            Assert.AreEqual("<Mouse>/rightButton", command.bindings[0].path);
        }

        [Test]
        public void TheRightMouseButton_IsNotBoundToAnotherWorldMapAction()
        {
            InputActionMap map = InputSystem.actions.FindActionMap("WorldMap", true);

            foreach (InputBinding binding in map.bindings)
            {
                if (binding.path == "<Mouse>/rightButton")
                {
                    Assert.AreEqual("Command", binding.action);
                }
            }
        }
    }
}
