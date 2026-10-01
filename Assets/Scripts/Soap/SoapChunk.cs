using UnityEngine;

namespace SoapCarvers.Soap
{
    /// <summary>Marker on each chunk GameObject so raycast hits can find the block.</summary>
    public class SoapChunk : MonoBehaviour
    {
        public SoapBlock Block { get; private set; }
        public Vector3Int Coord { get; private set; }

        public void Init(SoapBlock block, Vector3Int coord)
        {
            Block = block;
            Coord = coord;
        }
    }
}
