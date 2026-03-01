using System;
using PurrNet;
using PurrNet.StateMachine;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProgressiveSample
{
    public class PlayerMovement : StateNode
    {
        [SerializeField] private float _acceleration = 50f;
        [SerializeField] private float _deceleration = 40f;
        [SerializeField] private float _rotationSpeed = 720f;
        [SerializeField] private PlayerDash _dashState;
        [SerializeField] private PlayerSuck _suckState;
        [SerializeField] private NetworkAnimator _netAnim;

        private Rigidbody _rb;
        private Vector3 _lastMoveDirection = Vector3.forward;
        private Vector3 _currentVelocity;
        private Transform _audioSourceTransform;

        private void Awake()
        {
            if (!TryGetComponent(out _rb))
                Debug.LogError("PlayerMovement failed to get Rigidbody component!", this);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            if (isOwner)
            {
                _audioSourceTransform = new GameObject("LocalAudio").transform;
                _audioSourceTransform.gameObject.AddComponent<AudioListener>();
            }
        }

        public override void Enter()
        {
            base.Enter();
            if (!isOwner) return;
            InputManager.Instance.GameControls.Attack.performed += OnAttack;
            InputManager.Instance.GameControls.Suck.performed += OnSuck;
        }

        public override void Exit()
        {
            if (!isOwner) return;
            InputManager.Instance.GameControls.Attack.performed -= OnAttack;
            InputManager.Instance.GameControls.Suck.performed -= OnSuck;
            if(_netAnim)
                _netAnim.SetBool("Moving", false);
        }

        public override void StateUpdate()
        {
            if (!isOwner) return;

            HandlePlayerMovement();
        }

        private void Update()
        {
            if(_audioSourceTransform)
                _audioSourceTransform.position = transform.position;
        }

        private float MoveSpeed => PlayerUpgrades.MoveSpeed;

        public void HandlePlayerMovement(bool movement = true, bool rotation = true)
        {
            Vector2 input = InputManager.Instance.GameControls.Move.ReadValue<Vector2>();
            Vector3 targetDirection = new Vector3(input.x, 0f, input.y).normalized;
            Vector3 targetVelocity = targetDirection * MoveSpeed;

            float accel = targetDirection.sqrMagnitude > 0.01f ? _acceleration : _deceleration;
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, accel * Time.deltaTime);
            
            if(movement)
                _rb.linearVelocity = new Vector3(_currentVelocity.x, _rb.linearVelocity.y, _currentVelocity.z);

            if (rotation && targetDirection.sqrMagnitude > 0.01f)
            {
                if(_netAnim && isCurrentState)
                    _netAnim.SetBool("Moving", true);
                _lastMoveDirection = targetDirection;
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                _rb.rotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
            else
            {
                if(_netAnim)
                    _netAnim.SetBool("Moving", false);
            }
        }

        private void OnAttack(InputAction.CallbackContext obj)
        {
            machine.SetState(_dashState);
        }

        private void OnSuck(InputAction.CallbackContext obj)
        {
            machine.SetState(_suckState);
        }
    }
}