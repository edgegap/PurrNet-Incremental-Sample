using System;
using System.Collections;
using PurrNet.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProgressiveSample
{
    [DefaultExecutionOrder(-100)]
    public class InputManager : MonoBehaviour
    {
        private static InputManager _instance;
        public static InputManager Instance
        {
            get
            {
                if (!_instance && !quitting)
                {
                    _instance = FindFirstObjectByType<InputManager>();
                
                    if (!_instance)
                    {
                        Debug.LogError("No InputManager found in scene!");
                    }
                    else if (_instance._playerInput == null)
                    {
                        _instance.InitializeInputSystem();
                    }
                }
                return _instance;
            }
        }

        private PlayerInput _playerInput;

        public PlayerInput.GameActions GameControls => _playerInput.Game;
        public PlayerInput.MenuActions MenuControls => _playerInput.Menu;

        private bool IsGameActive => ((InputActionMap)GameControls).enabled;

        private static bool quitting;

#if UNITY_EDITOR
        [SerializeField, PurrReadOnly] private string _currentActionMap;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            Application.quitting -= OnQuit;
            quitting = false;
            _instance = null;
        }

        private static void OnQuit()
        {
            quitting = true;
        }

        private void Awake()
        {
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeInputSystem();
        }
        
        private void InitializeInputSystem()
        {
            if (_playerInput == null)
            {
                _playerInput = new PlayerInput();
            }
        }
        
        private void OnEnable()
        {
            _playerInput.Enable();
        }

        private void OnDisable()
        {
            if(_playerInput != null)
                _playerInput.Disable();
        }

        private Coroutine _switchRoutine;

        private void SwitchActionMap(InputActionMap actionMap, float transitionTime, Action onComplete)
        {
#if UNITY_EDITOR
            _currentActionMap = actionMap.name;
#endif
            if (transitionTime <= 0)
            {
                _playerInput.Disable();
                actionMap.Enable();
                onComplete?.Invoke();
                return;
            }

            if(_switchRoutine != null)
                StopCoroutine(_switchRoutine);
            _switchRoutine = StartCoroutine(SwitchActionMapTimed(actionMap, transitionTime, onComplete));
        }

        private IEnumerator SwitchActionMapTimed(InputActionMap actionMap, float transitionTime, Action onComplete)
        {
            _playerInput.Disable();

            float t = 0;
            while (t < transitionTime)
            {
                t += Time.deltaTime;
                yield return null;
            }
            
            actionMap.Enable();
            onComplete?.Invoke();
            _switchRoutine = null;
        }

        public void EnableGameControls(float transitionTime = 0)
        {
            SwitchActionMap(_playerInput.Game, transitionTime, () =>
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            });
        }

        public void EnableMenuControls(float transitionTime = 0)
        {
            SwitchActionMap(_playerInput.Menu, transitionTime, () =>
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            });
        }

        public void StartRebinding(InputAction action, int bindingIndex, Action<InputAction, int> onComplete)
        {
            _playerInput.Disable();
            
            action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    _playerInput.Enable();
                    onComplete?.Invoke(action, bindingIndex);
                })
                .Start();
        }

        public string GetBindingOverridesJson()
        {
            return _playerInput.SaveBindingOverridesAsJson();
        }

        public void LoadBindingOverrides(string json)
        {
            _playerInput.LoadBindingOverridesFromJson(json);
        }

        public bool WasDigitPressed(int i)
        {
            if (i < 0 || i > 9) return false;
    
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
    
            return i switch
            {
                0 => keyboard.digit0Key.wasPressedThisFrame,
                1 => keyboard.digit1Key.wasPressedThisFrame,
                2 => keyboard.digit2Key.wasPressedThisFrame,
                3 => keyboard.digit3Key.wasPressedThisFrame,
                4 => keyboard.digit4Key.wasPressedThisFrame,
                5 => keyboard.digit5Key.wasPressedThisFrame,
                6 => keyboard.digit6Key.wasPressedThisFrame,
                7 => keyboard.digit7Key.wasPressedThisFrame,
                8 => keyboard.digit8Key.wasPressedThisFrame,
                9 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };
        }
    }
}