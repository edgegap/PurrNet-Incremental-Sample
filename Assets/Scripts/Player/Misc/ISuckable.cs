using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public interface ISuckable
    {
        NetworkIdentity networkIdentity { get; }
        Rigidbody rigidbody { get; }
        void SetVisible(bool visible);
    }
}