using System.Collections.Generic;
using System.Text;
using BuildCrew.Parts;
using UnityEngine;
using UnityEngine.UI;

namespace BuildCrew.Sorting
{
    /// <summary>
    /// A labeled pallet area for one category (wood, stone, roof, glass,
    /// ready-made, fixings). Every few frames it checks which loose parts sit in
    /// its box and shows live counts ("Planks 2 m: 7") on a world-space label.
    /// It is a child of the pallet (a rigidbody), so dragging the pallet moves
    /// the zone. Purely an observer: it never changes game state.
    /// </summary>
    public class SortingZone : MonoBehaviour
    {
        [SerializeField] PartCategory category;
        [SerializeField] string title = "Pallet";
        [SerializeField] Vector3 halfExtents = new Vector3(1.4f, 0.9f, 0.9f);
        [SerializeField] Text label;
        [SerializeField] int teamId;

        static readonly Collider[] Hits = new Collider[256];
        readonly HashSet<Part> _inside = new HashSet<Part>();
        readonly SortedDictionary<string, int> _counts = new SortedDictionary<string, int>();
        readonly StringBuilder _sb = new StringBuilder();
        float _timer;

        public PartCategory Category => category;
        public int TeamId => teamId;
        public IReadOnlyCollection<Part> Inside => _inside;

        public void Configure(PartCategory cat, string niceTitle, Vector3 half, Text labelText, int team)
        {
            category = cat;
            title = niceTitle;
            halfExtents = half;
            label = labelText;
            teamId = team;
        }

        public bool Contains(Part part) => _inside.Contains(part);

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.3f;
            Scan();
        }

        void Scan()
        {
            _inside.Clear();
            _counts.Clear();
            Vector3 center = transform.TransformPoint(0f, halfExtents.y, 0f);
            int n = Physics.OverlapBoxNonAlloc(center, halfExtents, Hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = Hits[i].attachedRigidbody;
                Part p = rb != null ? rb.GetComponent<Part>() : null;
                if (p == null || p.InBuilding || !_inside.Add(p)) continue;
                string key = p is FixingBox box ? $"{p.Definition.PluralName} ({box.Count} left)" : p.GroupLabel;
                _counts.TryGetValue(key, out int c);
                _counts[key] = c + 1;
            }
            if (label == null) return;
            _sb.Clear();
            _sb.Append("<b>").Append(title).Append("</b>");
            foreach (KeyValuePair<string, int> kv in _counts) _sb.Append('\n').Append(kv.Key).Append(": ").Append(kv.Value);
            if (_counts.Count == 0) _sb.Append("\n(empty)");
            label.text = _sb.ToString();
        }
    }
}
