using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // While aiming at a piece with the hammer, tints red the pieces that would fall if you removed it: while you
    // hold the fine-rotation key ("careful" mode) or have removal locked, so it isn't flashing while you build. Runs the
    // game's support rules (WearNTear.UpdateSupport) for the pieces around it twice, with and without the aimed
    // piece, and marks what stands now but wouldn't then.
    internal class RemovalPreview : MonoBehaviour
    {
        const float Range = 24f;          // pieces simulated around the aimed one
        const float OutsideRange = 30f;   // beyond Range, pieces keep their current support
        const float RefreshInterval = 3f; // and at once when you aim at another piece
        const int MaxIterations = 400;

        static readonly Color FallColor = new Color(1f, 0.1f, 0.05f);
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        delegate void MaterialProps(WearNTear wnt, out float maxSupport, out float minSupport, out float horizontalLoss, out float verticalLoss);
        static readonly MaterialProps GetMaterial = Create<MaterialProps>("GetMaterialProperties");
        static readonly Func<WearNTear, float> GetSupport = Create<Func<WearNTear, float>>("GetSupport");

        static T Create<T>(string name) where T : Delegate
        {
            try { return AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(WearNTear), name)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Norse Sense] Removal preview unavailable ({name}): {e.Message}");
                return null;
            }
        }

        // Shown in the build info box; null when not aiming at a piece that holds others up.
        internal static InfoRow Line;

        class Node
        {
            internal WearNTear Wnt;
            internal Vector3 Com;
            internal float Max, Min, Horizontal, Vertical;
            internal bool Grounded;
            internal readonly List<Edge> Edges = new List<Edge>();
        }

        struct Edge
        {
            internal int To;          // index into _nodes, or -1 for a piece outside the simulated area
            internal float Fixed;     // support of that outside piece
            internal float Distance;  // num6 in UpdateSupport
            internal bool Below;      // the support point is under the centre of mass
            internal bool Angled;     // ... and lower than it, so the angle loss applies
            internal float AngleT;
            internal Vector3 Point;
        }

        static int _rayMask, _terrainLayer = -1;
        static readonly Collider[] Hits = new Collider[128];

        readonly List<Node> _nodes = new List<Node>();
        readonly Dictionary<WearNTear, int> _index = new Dictionary<WearNTear, int>();
        readonly List<WearNTear> _falling = new List<WearNTear>();
        WearNTear _aimed;
        float _timer;

        void Update()
        {
            var player = Player.m_localPlayer;
            bool wanted = Plugin.ShowRemovalPreview.Value && (BuildHelpers.FineModifierHeld() || RemovalLockPatch.Locked);
            var piece = player != null && player.InPlaceMode() && wanted && GetMaterial != null && GetSupport != null
                ? player.GetHoveringPiece() : null;
            var wnt = piece != null && piece.CanBeRemoved() ? piece.GetComponent<WearNTear>() : null;
            if (wnt == null || !wnt.m_supports)
            {
                Clear();
                return;
            }

            _timer -= Time.deltaTime;
            if (wnt == _aimed && _timer > 0f) return;
            _timer = RefreshInterval;
            _aimed = wnt;

            Untint();
            Simulate(wnt);
            foreach (var f in _falling)
                if (f != null) Tint(f);
            Line = _falling.Count == 0 ? null
                : new InfoRow("Removing", $"<b>{_falling.Count}</b> piece{(_falling.Count == 1 ? "" : "s")} would fall", "tinted red", Tone.Warn);
        }

        void OnDisable() => Clear();

        void Clear()
        {
            Untint();
            _aimed = null;
            Line = null;
        }

        void Untint()
        {
            foreach (var f in _falling)
            {
                if (f == null) continue;
                MaterialMan.instance.ResetValue(f.gameObject, ColorId);
                MaterialMan.instance.ResetValue(f.gameObject, EmissionId);
                RepairView.Retint(f); // keep the repairs view's tint if it had one
            }
            _falling.Clear();
        }

        static void Tint(WearNTear wnt)
        {
            MaterialMan.instance.SetValue(wnt.gameObject, ColorId, FallColor);
            MaterialMan.instance.SetValue(wnt.gameObject, EmissionId, FallColor * 0.6f);
        }

        // ---------- Simulation ----------

        void Simulate(WearNTear removed)
        {
            if (_rayMask == 0)
            {
                _rayMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
                _terrainLayer = LayerMask.NameToLayer("terrain");
            }
            Build(removed.transform.position);
            if (!_index.TryGetValue(removed, out int skip)) return;

            var before = Fallen(-1);
            var after = Fallen(skip);
            for (int i = 0; i < _nodes.Count; i++)
                if (i != skip && after[i] && !before[i]) _falling.Add(_nodes[i].Wnt);
        }

        void Build(Vector3 center)
        {
            _nodes.Clear();
            _index.Clear();
            foreach (var wnt in WearNTear.GetAllInstances())
            {
                if (wnt == null || Vector3.Distance(wnt.transform.position, center) > Range) continue;
                GetMaterial(wnt, out float max, out float min, out float h, out float v);
                _index[wnt] = _nodes.Count;
                _nodes.Add(new Node
                {
                    Wnt = wnt, Com = wnt.transform.position + wnt.transform.rotation * wnt.m_comOffset,
                    Max = max, Min = min, Horizontal = h, Vertical = v
                });
            }
            foreach (var node in _nodes) Connect(node);
        }

        // WearNTear.UpdateSupport's search: each collider's box, grown by 0.3 m, against what touches it.
        void Connect(Node node)
        {
            var own = node.Wnt.GetComponentsInChildren<Collider>(true);
            foreach (var col in own)
            {
                if (col.isTrigger || col.attachedRigidbody != null) continue;
                Vector3 pos, size;
                Quaternion rot;
                if (col is BoxCollider box)
                {
                    rot = box.transform.rotation;
                    pos = box.transform.position + box.transform.TransformVector(box.center);
                    Vector3 s = box.transform.lossyScale;
                    size = new Vector3(s.x * box.size.x, s.y * box.size.y, s.z * box.size.z);
                }
                else
                {
                    rot = Quaternion.identity;
                    pos = col.bounds.center;
                    size = col.bounds.size;
                }
                size = (size + Vector3.one * 0.3f) * 0.5f;

                int n = Physics.OverlapBoxNonAlloc(pos, size, Hits, rot, _rayMask);
                for (int k = 0; k < n; k++)
                {
                    var other = Hits[k];
                    if (other.attachedRigidbody != null || other.isTrigger || Array.IndexOf(own, other) >= 0) continue;
                    if (other.gameObject.layer == _terrainLayer)
                    {
                        node.Grounded = true;
                        continue;
                    }
                    var otherWnt = other.GetComponentInParent<WearNTear>();
                    if (otherWnt == null)
                    {
                        node.Grounded = true; // rock or other solid world object
                        continue;
                    }
                    if (!otherWnt.m_supports || otherWnt == node.Wnt) continue;

                    Vector3 otherCom = otherWnt.transform.position + otherWnt.transform.rotation * otherWnt.m_comOffset;
                    float d = Vector3.Distance(node.Com, otherCom) + 0.1f;
                    float d2 = Vector3.Distance(node.Com, otherWnt.transform.position) + 0.1f;
                    if (d2 < d && !node.Wnt.m_forceCorrectCOMCalculation) d = d2;

                    var edge = new Edge { Distance = d };
                    if (_index.TryGetValue(otherWnt, out int to)) edge.To = to;
                    else
                    {
                        edge.To = -1;
                        edge.Fixed = Vector3.Distance(otherWnt.transform.position, node.Wnt.transform.position) < OutsideRange
                            ? GetSupport(otherWnt) : 0f;
                    }
                    Vector3 point = SupportPoint(node.Com, otherCom, other);
                    if (point.y < node.Com.y + 0.05f)
                    {
                        edge.Below = true;
                        edge.Point = point;
                        Vector3 dir = (point - node.Com).normalized;
                        if (dir.y < 0f)
                        {
                            edge.Angled = true;
                            edge.AngleT = Mathf.Acos(1f - Mathf.Abs(dir.y)) / (Mathf.PI / 2f);
                        }
                    }
                    node.Edges.Add(edge);
                }
            }
        }

        static Vector3 SupportPoint(Vector3 com, Vector3 otherCom, Collider other)
        {
            if (other is MeshCollider mesh && !mesh.convex)
                return mesh.Raycast(new Ray(com, Vector3.down), out RaycastHit hit, 10f) ? hit.point : (com + otherCom) * 0.5f;
            return other.ClosestPoint(com);
        }

        // Which pieces end up below their minimum support with `skip` gone (-1 = nothing removed). Support spreads up
        // from the ground until nothing changes; pieces that fall stop holding others, so it repeats until stable.
        bool[] Fallen(int skip)
        {
            int count = _nodes.Count;
            var gone = new bool[count];
            var support = new float[count];
            if (skip >= 0) gone[skip] = true;

            var points = new List<Vector3>();
            var values = new List<float>();
            for (int round = 0; round < 10; round++)
            {
                for (int i = 0; i < count; i++) support[i] = !gone[i] && _nodes[i].Grounded ? _nodes[i].Max : 0f;

                for (int iter = 0; iter < MaxIterations; iter++)
                {
                    bool changed = false;
                    for (int i = 0; i < count; i++)
                    {
                        var node = _nodes[i];
                        if (gone[i] || node.Grounded) continue;
                        float best = 0f;
                        points.Clear();
                        values.Clear();
                        foreach (var e in node.Edges)
                        {
                            float s = e.To >= 0 ? (gone[e.To] ? 0f : support[e.To]) : e.Fixed;
                            if (s <= 0f) continue;
                            best = Mathf.Max(best, s - node.Horizontal * e.Distance * s);
                            if (!e.Below) continue;
                            if (e.Angled)
                                best = Mathf.Max(best, s - Mathf.Lerp(node.Horizontal, node.Vertical, e.AngleT) * e.Distance * s);
                            points.Add(e.Point);
                            values.Add(s - node.Vertical * e.Distance * s);
                        }
                        // Two supports on roughly opposite sides hold it like a bridge.
                        for (int a = 0; a < points.Count - 1; a++)
                        {
                            Vector3 from = points[a] - node.Com;
                            from.y = 0f;
                            for (int b = a + 1; b < points.Count; b++)
                            {
                                float avg = (values[a] + values[b]) * 0.5f;
                                if (avg <= best) continue;
                                Vector3 to = points[b] - node.Com;
                                to.y = 0f;
                                if (Vector3.Angle(from, to) >= 100f) best = avg;
                            }
                        }
                        best = Mathf.Min(best, node.Max);
                        if (best > support[i] + 0.001f)
                        {
                            support[i] = best;
                            changed = true;
                        }
                    }
                    if (!changed) break;
                }

                bool fell = false;
                for (int i = 0; i < count; i++)
                {
                    if (gone[i] || support[i] >= _nodes[i].Min) continue;
                    gone[i] = true;
                    fell = true;
                }
                if (!fell) break;
            }
            return gone;
        }
    }
}
