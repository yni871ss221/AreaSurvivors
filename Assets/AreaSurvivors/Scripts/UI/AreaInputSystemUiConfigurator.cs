using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AreaSurvivors
{
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class AreaInputSystemUiConfigurator : MonoBehaviour
    {
        InputActionAsset runtimeActions;
        readonly List<InputActionReference> references = new List<InputActionReference>();

        void Awake()
        {
            Configure(GetComponent<InputSystemUIInputModule>());
        }

        void OnDestroy()
        {
            foreach (InputActionReference reference in references)
            {
                if (reference != null) Destroy(reference);
            }

            references.Clear();
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        void Configure(InputSystemUIInputModule module)
        {
            if (module == null) return;

            runtimeActions = ScriptableObject.CreateInstance<InputActionAsset>();
            runtimeActions.name = "Area Survivors UI Input";
            var map = new InputActionMap("UI");
            runtimeActions.AddActionMap(map);

            InputAction navigate = map.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");
            AddKeyboardNavigation(navigate);

            InputAction submit = map.AddAction("Submit", InputActionType.Button, expectedControlLayout: "Button");
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");

            InputAction cancel = map.AddAction("Cancel", InputActionType.Button, expectedControlLayout: "Button");
            InputAction point = AddAction(map, "Point", "Vector2", "<Mouse>/position");
            InputAction click = AddAction(map, "Click", "Button", "<Mouse>/leftButton");
            InputAction middleClick = AddAction(map, "MiddleClick", "Button", "<Mouse>/middleButton");
            InputAction rightClick = AddAction(map, "RightClick", "Button", "<Mouse>/rightButton");
            InputAction scroll = AddAction(map, "ScrollWheel", "Vector2", "<Mouse>/scroll");
            InputAction trackedPosition = map.AddAction("TrackedDevicePosition", InputActionType.PassThrough, expectedControlLayout: "Vector3");
            InputAction trackedOrientation = map.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough, expectedControlLayout: "Quaternion");

            module.actionsAsset = runtimeActions;
            module.move = Reference(navigate);
            module.submit = Reference(submit);
            module.cancel = Reference(cancel);
            module.point = Reference(point);
            module.leftClick = Reference(click);
            module.middleClick = Reference(middleClick);
            module.rightClick = Reference(rightClick);
            module.scrollWheel = Reference(scroll);
            module.trackedDevicePosition = Reference(trackedPosition);
            module.trackedDeviceOrientation = Reference(trackedOrientation);
        }

        InputActionReference Reference(InputAction action)
        {
            InputActionReference reference = InputActionReference.Create(action);
            references.Add(reference);
            return reference;
        }

        static InputAction AddAction(InputActionMap map, string name, string controlType, string binding)
        {
            InputAction action = map.AddAction(name, InputActionType.PassThrough, expectedControlLayout: controlType);
            action.AddBinding(binding);
            return action;
        }

        static void AddKeyboardNavigation(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

    }
}
