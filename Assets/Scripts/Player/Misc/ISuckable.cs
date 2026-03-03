using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public interface ISuckable
    {
        NetworkIdentity networkIdentity { get; }
        Rigidbody rigidbody { get; }
        int prefabId { get; }
    }
}