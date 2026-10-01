// =============================================================================
// FUTURE TOOL (not implemented): 2-player "Wire Cutter"
// =============================================================================
// A clay-cutting wire with a handle at each end. Two players each hold one
// handle; while BOTH hold LMB, the soap between their hands is carved along the
// wire. It is the reason SoapCarveCommand supports capsules.
//
// Extension points already in place:
//   * SoapBlock.CarveCapsule(handleA, handleB, radius, sourceId)
//     -> builds a SoapCarveCommand and goes through SoapBlock.ApplyCarve.
//   * ItemManager / IItemHolder: one holder per handle. The cutter would be two
//     Holdables (handle A, handle B) sharing a WireCutterLink component, so the
//     usual pick up / drop / throw commands keep working per handle.
//   * Tool.StartUse / StopUse: each handle reports "pulling" to the link.
//
// Sketch:
//
//   public class WireCutterHandle : Tool
//   {
//       [SerializeField] WireCutterLink link;   // shared by both handles
//       public override void Use() { }          // continuous, see link
//       protected override void OnStartUse() => link.SetPulling(this, true);
//       protected override void OnStopUse()  => link.SetPulling(this, false);
//   }
//
//   public class WireCutterLink : MonoBehaviour  // draws the wire (LineRenderer)
//   {
//       public WireCutterHandle A, B;
//       // Every ~0.05 s while both handles are held by DIFFERENT players and both pull:
//       //   soap.CarveCapsule(A.transform.position, B.transform.position, 0.05f, A.OwnerId);
//       // Optionally snap/"break" the wire if the hands get further apart than maxLength.
//   }
//
// Networking: the host should be the one that issues the capsule commands, since
// it is the only peer that knows both handle positions authoritatively.
// =============================================================================

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Marker interface for future tools that need more than one player
    /// (see the comment block at the top of this file).
    /// </summary>
    public interface ICooperativeTool
    {
        /// <summary>How many holders must take part for the tool to work.</summary>
        int RequiredHolders { get; }
    }
}
