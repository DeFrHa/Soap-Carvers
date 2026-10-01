using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>Something heavy players can shove by walking into it (e.g. rolling scaffolding).</summary>
    public interface IPushable
    {
        /// <summary>Called every frame the player walks into it. Direction is horizontal and normalized.</summary>
        void Push(Vector3 direction);
    }
}
