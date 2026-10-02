using BuildCrew.Core;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Building
{
    public enum SlotState
    {
        Empty = 0,
        Snapped = 1,
        Fixed = 2,
    }

    /// <summary>
    /// One place in the building, at the slot pose (child of the BuildSite).
    /// Shows a transparent ghost box while empty. Holds the part it contains:
    ///   Snapped: a soft ConfigurableJoint pulls the part to the slot pose.
    ///   Fixed:   FixedJoints (with break force) to the parts it rests on, or to
    ///            the world for ground-level slots.
    /// Everything stays a dynamic rigidbody. State changes only via BuildManager.
    /// </summary>
    public class BuildSlot : MonoBehaviour
    {
        KitSlot _data;
        MeshRenderer _ghost;
        Rigidbody _anchor;
        ConfigurableJoint _softJoint;
        Quaternion _anchorTarget;
        int _anchorSteps;

        public BuildSite Site { get; private set; }
        public int Index { get; private set; }
        public KitSlot Data => _data;
        public string Id => _data.id;
        public int Stage => _data.stage;
        public Vector3 Size => _data.size;
        public FixMethod Method { get; private set; }
        public PartDefinition Definition { get; private set; }
        public BuildSlot[] RestsOn { get; private set; } = new BuildSlot[0];
        public SlotState State { get; private set; }
        public Part Part { get; private set; }
        public int FixProgress { get; private set; }
        public MeshRenderer Ghost => _ghost;

        /// <summary>Pose a snapped part is pulled to (world).</summary>
        public Vector3 TargetPosition => transform.position;
        public Quaternion TargetRotation { get; private set; }

        /// <summary>All slots this rests on are fixed (ground slots: always).</summary>
        public bool RestsSatisfied
        {
            get
            {
                foreach (BuildSlot r in RestsOn)
                    if (r.State != SlotState.Fixed) return false;
                return true;
            }
        }

        /// <summary>Empty and ready to accept a part.</summary>
        public bool IsActive => State == SlotState.Empty && RestsSatisfied;

        public int FixNeeded(GameSettings s) =>
            Method == FixMethod.Nails ? s.nailHitsPerFix : Method == FixMethod.Screws ? s.screwsPerFix : 1;

        public void Initialize(BuildSite site, int index, KitSlot data, Material ghostMaterial)
        {
            Site = site;
            Index = index;
            _data = data;
            Method = KitDefinition.ParseFixMethod(data.fixMethod);
            Definition = PartCatalog.Get(data.partType);

            var ghostGo = new GameObject("Ghost");
            ghostGo.transform.SetParent(transform, false);
            ghostGo.transform.localScale = data.size;
            ghostGo.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Cube);
            _ghost = ghostGo.AddComponent<MeshRenderer>();
            _ghost.sharedMaterial = ghostMaterial;
            _ghost.shadowCastingMode = ShadowCastingMode.Off;
            _ghost.receiveShadows = false;
            TargetRotation = transform.rotation;
        }

        public void ResolveRestsOn(BuildSlot[] restsOn) => RestsOn = restsOn;

        public void SetGhostMaterial(Material m)
        {
            bool show = m != null;
            if (_ghost.enabled != show) _ghost.enabled = show;
            if (show && _ghost.sharedMaterial != m) _ghost.sharedMaterial = m;
        }

        /// <summary>Does this part fit (type and size), and how should it sit?</summary>
        public bool TryMatch(Part part, GameSettings s, out Quaternion targetRotation, out float angle)
        {
            targetRotation = transform.rotation;
            angle = float.MaxValue;
            if (part == null || part.TypeId != _data.partType) return false;
            return SlotMatcher.TryMatch(part.Size, _data.size, part.transform.rotation, transform.rotation, s.sizeTolerance,
                out targetRotation, out angle);
        }

        // ================================================= BuildManager only

        public void Snap(Part part, Quaternion targetRotation, GameSettings s)
        {
            Part = part;
            State = SlotState.Snapped;
            FixProgress = 0;
            TargetRotation = targetRotation;
            Rigidbody body = part.Body;

            // Kinematic anchor at the slot. It starts with the part's CURRENT rotation,
            // so the joint's reference is "as it is now", then turns to the target
            // rotation (FixedUpdate) and the slerp drive follows.
            var anchorGo = new GameObject($"SnapAnchor {Id}");
            anchorGo.transform.SetParent(transform, false);
            _anchor = anchorGo.AddComponent<Rigidbody>();
            _anchor.isKinematic = true;
            _anchor.useGravity = false;
            // Raise the anchor by the spring's static sag (g / f^2) so the part rests exactly in the slot.
            float sag = -Physics.gravity.y / (s.snapFrequency * s.snapFrequency);
            Vector3 anchorPos = TargetPosition + Vector3.up * sag;
            anchorGo.transform.SetPositionAndRotation(anchorPos, part.transform.rotation);
            _anchor.position = anchorPos;
            _anchor.rotation = part.transform.rotation;
            _anchorTarget = targetRotation;
            _anchorSteps = 0;

            _softJoint = part.gameObject.AddComponent<ConfigurableJoint>();
            _softJoint.autoConfigureConnectedAnchor = false;
            _softJoint.connectedBody = _anchor;
            _softJoint.anchor = Vector3.zero;
            _softJoint.connectedAnchor = Vector3.zero;
            _softJoint.xMotion = _softJoint.yMotion = _softJoint.zMotion = ConfigurableJointMotion.Free;
            _softJoint.angularXMotion = _softJoint.angularYMotion = _softJoint.angularZMotion = ConfigurableJointMotion.Free;
            _softJoint.enablePreprocessing = false;

            float m = body.mass;
            float k = m * s.snapFrequency * s.snapFrequency;
            var linear = new JointDrive
            {
                positionSpring = k,
                positionDamper = 2f * Mathf.Sqrt(k * m),
                maximumForce = m * -Physics.gravity.y * s.snapForceWeights + 150f,
            };
            _softJoint.xDrive = linear;
            _softJoint.yDrive = linear;
            _softJoint.zDrive = linear;

            Vector3 it = body.inertiaTensor;
            float inertia = (it.x + it.y + it.z) / 3f;
            float ka = inertia * s.snapFrequency * s.snapFrequency * 1.5f;
            float maxDim = Mathf.Max(part.Size.x, Mathf.Max(part.Size.y, part.Size.z));
            _softJoint.rotationDriveMode = RotationDriveMode.Slerp;
            _softJoint.slerpDrive = new JointDrive
            {
                positionSpring = ka,
                positionDamper = 2f * Mathf.Sqrt(ka * inertia),
                maximumForce = m * -Physics.gravity.y * maxDim + 50f,
            };
            body.WakeUp();
        }

        public void Fix(GameSettings s)
        {
            if (Part == null) return;
            RemoveSoftJoint();
            Rigidbody body = Part.Body;

            // Nudge into the exact pose if it's close (keeps stacked walls straight);
            // a crooked placement stays crooked.
            if (Vector3.Distance(body.position, TargetPosition) < 0.04f && Quaternion.Angle(body.rotation, TargetRotation) < 4f)
            {
                body.position = TargetPosition;
                body.rotation = TargetRotation;
                Part.transform.SetPositionAndRotation(TargetPosition, TargetRotation);
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            float force, torque;
            switch (Method)
            {
                case FixMethod.Mortar: force = s.mortarBreakForce; torque = s.mortarBreakTorque; break;
                case FixMethod.Screws: force = s.screwBreakForce; torque = s.screwBreakTorque; break;
                default: force = s.nailBreakForce; torque = s.nailBreakTorque; break;
            }

            int joined = 0;
            foreach (BuildSlot r in RestsOn)
            {
                if (r.Part == null || r.State != SlotState.Fixed) continue;
                AddFixedJoint(r.Part.Body, force, torque);
                joined++;
            }
            if (joined == 0) AddFixedJoint(null, force, torque); // ground slot: anchored to the world
            State = SlotState.Fixed;
            FixProgress = FixNeeded(s);
        }

        void AddFixedJoint(Rigidbody other, float force, float torque)
        {
            var j = Part.gameObject.AddComponent<FixedJoint>();
            j.connectedBody = other;
            j.breakForce = force;
            j.breakTorque = torque;
            j.enableCollision = false;
            Part.AddFixJoint(j);
        }

        public void AddFixProgress(int amount) => FixProgress += amount;

        /// <summary>Empties the slot, removing its joints. Returns the part that was in it.</summary>
        public Part Clear()
        {
            Part p = Part;
            RemoveSoftJoint();
            if (p != null) p.ClearFixJoints();
            Part = null;
            State = SlotState.Empty;
            FixProgress = 0;
            TargetRotation = transform.rotation;
            return p;
        }

        void RemoveSoftJoint()
        {
            if (_softJoint != null) Destroy(_softJoint);
            if (_anchor != null) Destroy(_anchor.gameObject);
            _softJoint = null;
            _anchor = null;
        }

        void FixedUpdate()
        {
            if (_anchor == null) return;
            // Two steps' grace so the joint has captured its reference rotation.
            if (++_anchorSteps < 3) return;
            Quaternion next = Quaternion.RotateTowards(_anchor.rotation, _anchorTarget, 200f * Time.fixedDeltaTime);
            if (next != _anchor.rotation) _anchor.MoveRotation(next);
        }
    }
}
