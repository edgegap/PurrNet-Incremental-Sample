using System;
using PurrNet;
using Unity.Cinemachine;
using UnityEngine;

namespace ProgressiveSample
{
    public class PlayerCamera : PlayerIdentity<PlayerCamera>
    {
        [SerializeField] private float _zoomFov = 30;
        [SerializeField] private float _fovZoomTime = 2f;
        [SerializeField] private AnimationCurve _fovZoomCurve;
        [SerializeField] private float _fovReverseZoomTime = 0.65f;
        [SerializeField] private AnimationCurve _fovZoomOutCurve;
        
        private CinemachineCamera _camera;

        private float _defaultFov;

        private bool _zooming;
        private float _zoomInTime;
        private float _zoomOutTime;
        private float _zoomOutStartFov;

        private void Awake()
        {
            if (!TryGetComponent(out _camera))
            {
                Debug.LogError($"Failed to get player camera!", this);
                return;
            }

            _defaultFov = _camera.Lens.FieldOfView;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _camera.Priority.Enabled = true;
            _camera.Priority.Value = isOwner ? 10 : 0;

            if (isServer)
            {
                if(InstanceHandler.TryGetInstance(out ResourceSpawner resourceSpawner))
                    resourceSpawner.RegisterPlayer(transform);
            }
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();
            
            if(InstanceHandler.TryGetInstance(out ResourceSpawner resourceSpawner))
                resourceSpawner.UnregisterPlayer(transform);
        }

        private void Update()
        {
            if (_zooming)
            {
                _zoomInTime += Time.deltaTime;
                var step = Mathf.Clamp01(_zoomInTime / _fovZoomTime);
                var eval = _fovZoomCurve.Evaluate(step);
                _camera.Lens.FieldOfView = Mathf.Lerp(_defaultFov, _zoomFov, eval);
                return;
            }

            if (_zoomOutTime < _fovReverseZoomTime)
            {
                _zoomOutTime += Time.deltaTime;
                var step = Mathf.Clamp01(_zoomOutTime / _fovReverseZoomTime);
                var eval = _fovZoomOutCurve.Evaluate(step);
                _camera.Lens.FieldOfView = Mathf.Lerp(_zoomOutStartFov, _defaultFov, eval);
            }
        }

        public void StartZoom()
        {
            _zooming = true;
            _zoomInTime = 0;
        }

        public void StopZoom()
        {
            _zooming = false;
            _zoomOutStartFov = _camera.Lens.FieldOfView;
            _zoomOutTime = 0;
        }
    }
}
