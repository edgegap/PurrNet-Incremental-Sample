using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    [RequireComponent(typeof(Rigidbody))]
    public class Loot : NetworkIdentity, ISuckable
    {
        [SerializeField] private float _popForce = 5f;
        [SerializeField] private float _popUpwardBias = 0.5f;
        [SerializeField] private float _torqueForce = 10f;
        
        private Rigidbody _rigidbody;
        public NetworkIdentity networkIdentity => this;
        public new Rigidbody rigidbody => _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        protected override void OnPoolReset()
        {
            gameObject.SetActive(true);
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            ToggleColliders(true);
        }

        public void Pop(Vector3 origin)
        {
            Vector3 direction = (transform.position - origin).normalized;
            direction.y = Mathf.Abs(direction.y) + _popUpwardBias;
            direction.Normalize();
            
            _rigidbody.AddForce(direction * _popForce, ForceMode.VelocityChange);
            _rigidbody.AddTorque(Random.insideUnitSphere * _torqueForce, ForceMode.VelocityChange);
        }

        public void PopRandom()
        {
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y) + _popUpwardBias;
            direction.Normalize();
            
            _rigidbody.AddForce(direction * _popForce, ForceMode.VelocityChange);
            _rigidbody.AddTorque(Random.insideUnitSphere * _torqueForce, ForceMode.VelocityChange);
        }

        public void ToggleColliders(bool toggle)
        {
            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = toggle;
            }
        }
    }
}