using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Core;
using BuildCrew.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Shared logic of the saw and the glass cutter: find the loose part under
    /// the crosshair, work out the cut (axis + position, snapped to lengths the
    /// building still needs), show a marker on the part and the resulting
    /// lengths on the HUD, and send a CutCommand when the work is done.
    /// </summary>
    public abstract class CuttingTool : Tool
    {
        protected const float Reach = 2.8f;

        static readonly List<float> NeededBuffer = new List<float>();

        GameObject _marker;
        string _status;

        protected Part Target { get; private set; }
        protected int Axis { get; private set; }
        protected float Offset { get; private set; }
        /// <summary>0-1 progress of the current cut.</summary>
        protected float Progress;

        protected abstract CutTool Kind { get; }
        protected abstract string MarkerMaterial { get; }
        protected abstract string Verb { get; }

        public override string StatusText => _status;

        /// <summary>Which local axis to cut across (0 = x, 1 = y) for this part, from the view.</summary>
        protected virtual int ChooseAxis(Part part) => 0;

        public override void Tick(Interaction.IGrabber holder, ToolInput input)
        {
            // While working, stay on the same part and cut position.
            if (!IsUsing || Target == null) Aim();
            base.Tick(holder, input);
            UpdateMarker();
            UpdateStatus();
        }

        void Aim()
        {
            Part part = AimedPart(Reach, out RaycastHit hit);
            if (part == null || part.InBuilding || part.Definition == null || part.Definition.CutTool != Kind)
            {
                if (Target != null) Progress = 0f;
                Target = null;
                return;
            }
            int axis = ChooseAxis(part);
            float offset = SnapOffset(part, axis, part.transform.InverseTransformPoint(hit.point)[axis]);
            if (part != Target || axis != Axis || Mathf.Abs(offset - Offset) > 0.02f) Progress = 0f;
            Target = part;
            Axis = axis;
            Offset = offset;
        }

        /// <summary>Clamp, round to 1 cm, and snap to a length the building still needs (from either end).</summary>
        float SnapOffset(Part part, int axis, float raw)
        {
            GameSettings s = Settings;
            float len = part.Size[axis];
            float half = len * 0.5f;
            float offset = Mathf.Clamp(raw, -half + s.minPieceLength, half - s.minPieceLength);
            offset = Mathf.Round((offset + half) * 100f) / 100f - half;

            NeededBuffer.Clear();
            CollectNeeded(part, axis, NeededBuffer);
            float best = s.cutSnapDistance;
            float snapped = offset;
            foreach (float n in NeededBuffer)
            {
                if (n >= len - s.sizeTolerance) continue;
                float fromNeg = -half + n, fromPos = half - n;
                if (Mathf.Abs(fromNeg - offset) < best) { best = Mathf.Abs(fromNeg - offset); snapped = fromNeg; }
                if (Mathf.Abs(fromPos - offset) < best) { best = Mathf.Abs(fromPos - offset); snapped = fromPos; }
            }
            return snapped;
        }

        /// <summary>Sizes along <paramref name="axis"/> of empty slots of this part's type (own team's sites).</summary>
        void CollectNeeded(Part part, int axis, List<float> into)
        {
            BuildManager build = World.Build;
            if (build == null) return;
            int team = Holder != null ? Holder.TeamId : 0;
            foreach (BuildSite site in build.Sites)
            {
                if (site == null || site.TeamId != team) continue;
                foreach (BuildSlot slot in site.Slots)
                {
                    if (slot.State != SlotState.Empty || slot.Data.partType != part.TypeId) continue;
                    into.Add(slot.Size[axis]);
                    if (Kind == CutTool.GlassCutter) into.Add(slot.Size[1 - axis]); // panes can be turned
                }
            }
        }

        void UpdateMarker()
        {
            bool show = Target != null && Holder != null;
            if (_marker == null)
            {
                if (!show) return;
                _marker = new GameObject($"{DisplayName} marker");
                _marker.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Cube);
                var r = _marker.AddComponent<MeshRenderer>();
                MaterialPalette p = World.Palette;
                r.sharedMaterial = p != null ? p.Get(MarkerMaterial) : MaterialPalette.Create(MarkerMaterial);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (_marker.activeSelf != show) _marker.SetActive(show);
            if (!show) return;
            Vector3 axis = Vector3.zero;
            axis[Axis] = 1f;
            Vector3 scale = Target.Size + Vector3.one * 0.02f;
            scale[Axis] = 0.008f;
            _marker.transform.SetPositionAndRotation(Target.transform.TransformPoint(axis * Offset), Target.transform.rotation);
            _marker.transform.localScale = scale;
        }

        void UpdateStatus()
        {
            if (Target == null)
            {
                _status = ToolsEnabled ? $"Aim at a loose {TargetNoun} to {Verb.ToLowerInvariant()} it" : null;
                return;
            }
            float len = Target.Size[Axis];
            float a = len * 0.5f + Offset, b = len - a;
            string pct = Progress > 0f ? $"  [{Progress * 100f:0}%]" : string.Empty;
            _status = $"{Verb}: {PartDefinition.FormatLength(a)} | {PartDefinition.FormatLength(b)}{pct}{ExtraStatus}";
        }

        protected virtual string TargetNoun => "part";
        protected virtual string ExtraStatus => string.Empty;

        protected void FinishCut()
        {
            if (Target == null) return;
            Send(new CutCommand { PartId = Target.EntityId, Axis = Axis, Offset = Offset });
            Target = null;
            Progress = 0f;
        }

        protected override void OnDropped()
        {
            Target = null;
            Progress = 0f;
            if (_marker != null) _marker.SetActive(false);
            _status = null;
        }

        protected override void OnDestroy()
        {
            if (_marker != null) Destroy(_marker);
            base.OnDestroy();
        }
    }
}
