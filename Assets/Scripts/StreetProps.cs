using System.Collections.Generic;
using UnityEngine;

// The street props that are drawn as a white outline over a black solid: trees, cars, benches, bins, traffic lights and the stroller (approved look: no grid lines inside the shapes).
// A prop is built from parts (a lofted body, a tube, a lumpy ball, a grid of rows). EndPart() turns a part into:
//  - an opaque black solid, set a little inside the surface, so lines behind it are hidden;
//  - outline lines: the silhouette seen from the player's resting eye position (the edges between a front-facing and a back-facing triangle), the open edges of sheets, and hard creases.
// Silhouettes are worked out once, from the head position at the final height, so they are exact from the bird's-eye view (during the rise they are only approximate).
public partial class StreetScene
{
    List<Vector3> pvv = new List<Vector3>(); List<int> ptt = new List<int>();
    Vector3 EyeRef => new Vector3(origin.x, 1.6f, origin.z);

    void BeginPart() { pvv.Clear(); ptt.Clear(); }
    int AV(Vector3 p) { pvv.Add(p); return pvv.Count - 1; }
    void AT(int a, int b, int c) { ptt.Add(a); ptt.Add(b); ptt.Add(c); }
    // rows of points; columns wrap round when closed; caps close the first and last row with a fan
    void AGrid(Vector3[][] rows, bool closed, bool cap0, bool cap1)
    {
        int R = rows.Length, C = rows[0].Length; var idx = new int[R][];
        for (int i = 0; i < R; i++) { idx[i] = new int[C]; for (int k = 0; k < C; k++) idx[i][k] = AV(rows[i][k]); }
        for (int i = 0; i < R - 1; i++)
            for (int k = 0; k < C; k++)
            {
                if (!closed && k + 1 >= C) continue;
                int k1 = (k + 1) % C;
                AT(idx[i][k], idx[i][k1], idx[i + 1][k1]); AT(idx[i][k], idx[i + 1][k1], idx[i + 1][k]);
            }
        for (int e = 0; e < 2; e++)
        {
            if (e == 0 ? !cap0 : !cap1) continue;
            int row = e == 0 ? 0 : R - 1; var cen = Vector3.zero; for (int k = 0; k < C; k++) cen += rows[row][k]; int cc = AV(cen / C);
            for (int k = 0; k < C; k++) AT(cc, idx[row][k], idx[row][(k + 1) % C]);
        }
    }

    // finish the part: weld, orient, draw silhouette / open / crease edges, add the inset solid (closed parts only)
    void EndPart(float inset, float crease)
    {
        var map = new Dictionary<Vector3Int, int>(); var wv = new List<Vector3>(); var wi = new int[pvv.Count];
        for (int i = 0; i < pvv.Count; i++)
        {
            var p = pvv[i]; var q = new Vector3Int(Mathf.RoundToInt(p.x * 500f), Mathf.RoundToInt(p.y * 500f), Mathf.RoundToInt(p.z * 500f));
            if (!map.TryGetValue(q, out int id)) { id = wv.Count; map[q] = id; wv.Add(p); }
            wi[i] = id;
        }
        var tr = new List<int[]>();
        for (int t = 0; t + 2 < ptt.Count; t += 3)
        {
            int a = wi[ptt[t]], b = wi[ptt[t + 1]], c = wi[ptt[t + 2]];
            if (a == b || b == c || a == c) continue;
            if (Vector3.Cross(wv[b] - wv[a], wv[c] - wv[a]).sqrMagnitude < 1e-10f) continue;
            tr.Add(new[] { a, b, c });
        }
        int nt = tr.Count; if (nt == 0) return;
        // edge -> triangles
        var edges = new Dictionary<long, List<int>>();
        for (int t = 0; t < nt; t++) for (int e = 0; e < 3; e++)
        {
            int u = tr[t][e], v = tr[t][(e + 1) % 3]; long key = u < v ? (long)u * 1000003L + v : (long)v * 1000003L + u;
            if (!edges.TryGetValue(key, out var l)) edges[key] = l = new List<int>(2);
            l.Add(t);
        }
        // consistent orientation by propagation over shared edges
        var seen = new bool[nt]; var stack = new Stack<int>();
        for (int s0 = 0; s0 < nt; s0++)
        {
            if (seen[s0]) continue; seen[s0] = true; stack.Push(s0);
            while (stack.Count > 0)
            {
                int t = stack.Pop();
                for (int e = 0; e < 3; e++)
                {
                    int u = tr[t][e], v = tr[t][(e + 1) % 3]; long key = u < v ? (long)u * 1000003L + v : (long)v * 1000003L + u;
                    var l = edges[key]; if (l.Count != 2) continue;
                    int o = l[0] == t ? l[1] : l[0]; if (seen[o]) continue;
                    bool same = false;                                                          // the neighbour must run the shared edge the other way round
                    for (int f = 0; f < 3; f++) if (tr[o][f] == u && tr[o][(f + 1) % 3] == v) same = true;
                    if (same) { int tmp = tr[o][1]; tr[o][1] = tr[o][2]; tr[o][2] = tmp; }
                    seen[o] = true; stack.Push(o);
                }
            }
        }
        bool closed = true; foreach (var kv in edges) if (kv.Value.Count != 2) { closed = false; break; }
        if (closed)                                                                             // make the orientation outward (positive volume)
        {
            float vol = 0f; foreach (var t in tr) vol += Vector3.Dot(wv[t[0]], Vector3.Cross(wv[t[1]], wv[t[2]]));
            if (vol < 0f) foreach (var t in tr) { int tmp = t[1]; t[1] = t[2]; t[2] = tmp; }
        }
        var nrm = new Vector3[nt]; var cen = new Vector3[nt];
        for (int t = 0; t < nt; t++) { var a = wv[tr[t][0]]; var b = wv[tr[t][1]]; var c = wv[tr[t][2]]; nrm[t] = Vector3.Cross(b - a, c - a).normalized; cen[t] = (a + b + c) / 3f; }
        var eye = EyeRef; float cosCrease = crease >= 179f ? -2f : Mathf.Cos(crease * Mathf.Deg2Rad);
        foreach (var kv in edges)
        {
            var l = kv.Value; int u = (int)(kv.Key / 1000003L), v = (int)(kv.Key % 1000003L); bool draw;
            if (l.Count == 1) draw = true;                                                      // the open edge of a sheet
            else if (l.Count == 2)
            {
                bool f1 = Vector3.Dot(nrm[l[0]], eye - cen[l[0]]) > 0f, f2 = Vector3.Dot(nrm[l[1]], eye - cen[l[1]]) > 0f;
                draw = f1 != f2 || Vector3.Dot(nrm[l[0]], nrm[l[1]]) < cosCrease;               // a silhouette edge or a hard crease
            }
            else draw = false;
            if (draw) L(wv[u], wv[v]);
        }
        if (!closed || inset <= 0f) return;
        var vn = new Vector3[wv.Count];
        for (int t = 0; t < nt; t++) { var an = Vector3.Cross(wv[tr[t][1]] - wv[tr[t][0]], wv[tr[t][2]] - wv[tr[t][0]]); for (int e = 0; e < 3; e++) vn[tr[t][e]] += an; }
        int b0 = sv.Count; for (int i = 0; i < wv.Count; i++) sv.Add(wv[i] - vn[i].normalized * inset);
        foreach (var t in tr) { si.Add(b0 + t[0]); si.Add(b0 + t[1]); si.Add(b0 + t[2]); }
    }

    // ---- part builders ----
    // a tube or cone from p0 (radius r0) to p1 (radius r1)
    void PCone(Vector3 p0, Vector3 p1, float r0, float r1, int n = 10, bool solid = true, float crease = 50f)
    {
        var w = (p1 - p0).normalized; var u = Vector3.Cross(w, Mathf.Abs(w.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var v = Vector3.Cross(w, u);
        var a = new Vector3[n]; var b = new Vector3[n];
        for (int i = 0; i < n; i++) { float t = i * Mathf.PI * 2f / n; var d = u * Mathf.Cos(t) + v * Mathf.Sin(t); a[i] = p0 + d * r0; b[i] = p1 + d * r1; }
        BeginPart(); AGrid(new[] { a, b }, true, true, true); EndPart(solid ? Mathf.Min(0.03f, 0.45f * Mathf.Min(r0, r1)) : 0f, crease);
    }
    // a lumpy ellipsoid (radii vary per point by +- wobble, flatter underneath): tree leaves, the stroller
    void PBlob(Vector3 c, Vector3 ua, Vector3 ub, float ra, float rv, float rb, float seed, float wobble, int sl, int st, float crease = 999f)
    {
        var rows = new Vector3[st + 1][];
        for (int j = 0; j <= st; j++)
        {
            rows[j] = new Vector3[sl]; float ph = Mathf.PI * j / st;
            for (int i = 0; i < sl; i++)
            {
                float th = Mathf.PI * 2f * i / sl, f = 1f + wobble * (Nz(seed, i, j) - 0.5f) * 2f, cy = Mathf.Cos(ph), flat = cy < 0f ? 0.75f : 1f;
                rows[j][i] = c + (ua * (Mathf.Sin(ph) * Mathf.Cos(th) * ra) + Vector3.up * (cy * rv * flat) + ub * (Mathf.Sin(ph) * Mathf.Sin(th) * rb)) * f;
            }
        }
        BeginPart(); AGrid(rows, true, false, false); EndPart(Mathf.Min(0.04f, 0.4f * Mathf.Min(ra, Mathf.Min(rv, rb))), crease);
    }
    static float Nz(float a, float b, float c) => Mathf.Repeat(Mathf.Abs(Mathf.Sin(a * 12.9898f + b * 78.233f + c * 37.719f) * 43758.5453f), 1f);
    // an axis-aligned (to ua, ub) box part
    void PBoxPart(Vector3 c, Vector3 ua, Vector3 ub, float la, float lb, float h0, float h1)
    {
        var a = ua * (la / 2); var b = ub * (lb / 2); var lo = c + Vector3.up * h0; var hi = c + Vector3.up * h1;
        BeginPart();
        var q = new Vector3[8]; int k = 0; for (int sx = -1; sx <= 1; sx += 2) for (int sy = 0; sy <= 1; sy++) for (int sz = -1; sz <= 1; sz += 2) q[k++] = (sy == 0 ? lo : hi) + a * sx + b * sz;
        int[] id = new int[8]; for (int i = 0; i < 8; i++) id[i] = AV(q[i]);
        int[][] f = { new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 } };
        foreach (var fc in f) { AT(id[fc[0]], id[fc[1]], id[fc[2]]); AT(id[fc[0]], id[fc[2]], id[fc[3]]); }
        EndPart(Mathf.Min(0.025f, 0.4f * Mathf.Min(la, Mathf.Min(lb, h1 - h0))), 30f);
    }
    void Poly(IList<Vector3> pts, bool closed = true) { for (int i = 0; i < pts.Count - (closed ? 0 : 1); i++) L(pts[i], pts[(i + 1) % pts.Count]); }
    static Vector3 X(Vector3 b, Vector3 ua, Vector3 ub, float x, float y, float z) => b + ua * x + Vector3.up * y + ub * z;

    // ---- tree: a bent tapering trunk, branches and seven lumpy leaf masses ----
    void Tree(Vector3 tb, float seed)
    {
        var side = right * Mathf.Cos(seed) + fwd * Mathf.Sin(seed); var side2 = right * Mathf.Sin(seed * 1.7f) + fwd * Mathf.Cos(seed * 1.7f);
        var mid = tb + Vector3.up * 1.2f + side * 0.1f; var top = tb + Vector3.up * 2.3f + side * 0.2f;
        PCone(tb, mid, 0.17f, 0.13f, 12); PCone(mid, top, 0.13f, 0.08f, 12);
        Vector3[] cs = { top + Vector3.up * 1.5f, top + side * 0.9f + Vector3.up * 1.1f + side2 * 0.1f, top - side * 0.8f + Vector3.up * 1.0f + side2 * 0.3f, top + side2 * 0.85f + Vector3.up * 1.5f, top - side2 * 0.85f + Vector3.up * 1.0f,
                         top + side * 0.5f + side2 * 0.5f + Vector3.up * 2.1f, top - side * 0.5f - side2 * 0.5f + Vector3.up * 1.9f };
        float[] rs = { 1.15f, 0.95f, 1.0f, 0.85f, 0.9f, 0.7f, 0.7f };
        for (int i = 0; i < cs.Length; i++)
        {
            PCone(top, cs[i] - Vector3.up * 0.4f, 0.06f, 0.03f, 5);
            PBlob(cs[i], right, fwd, rs[i], rs[i] * 0.85f, rs[i], seed + i * 1.3f + 3f, 0.17f, 14, 9);
        }
    }

    // ---- car: a lofted shell with flat sides, a belt line and a cabin, a window outline, door seams, mirrors, wheels with tyre, rim and spokes, lamps, a grille ----
    void Car(Vector3 c, Vector3 ua, Vector3 ub)
    {
        const int NS = 15; const float BELT = 0.88f;
        float[] tu = { -2.15f, -1.9f, -1.5f, -1.0f, -0.45f, 0.35f, 0.85f, 1.3f, 1.8f, 2.15f }, tt = { 0.62f, 0.86f, 0.93f, 1.2f, 1.45f, 1.45f, 1.2f, 0.92f, 0.84f, 0.55f };
        float[] wu = { -2.15f, -1.95f, -1.6f, 1.6f, 1.95f, 2.15f }, ww = { 1.45f, 1.65f, 1.76f, 1.76f, 1.66f, 1.45f };
        var rows = new Vector3[NS][]; var cab = new List<int>(); const int C = 16;
        for (int s = 0; s < NS; s++)
        {
            float u = Mathf.Lerp(-2.15f, 2.15f, s / (NS - 1f)), top = Interp(u, tu, tt), rx = Interp(u, wu, ww) / 2f, bot = Mathf.Abs(u) < 2f ? 0.3f : 0.42f, bl = Mathf.Min(BELT, top - 0.02f);
            float[] hl = new float[9], hy = new float[9];
            hl[0] = 0f; hy[0] = bot; hl[1] = 0.75f; hy[1] = bot; hl[2] = 0.98f; hy[2] = bot + 0.10f; hl[3] = 1f; hy[3] = Mathf.Max(bot + 0.14f, bl - 0.2f); hl[4] = 1f; hy[4] = bl;
            if (top > BELT + 0.08f) { cab.Add(s); hl[5] = 0.92f; hy[5] = BELT + 0.07f; hl[6] = 0.8f; hy[6] = BELT + (top - BELT) * 0.55f; hl[7] = 0.52f; hy[7] = top - 0.03f; }
            else { hl[5] = 0.9f; hy[5] = top - 0.03f; hl[6] = 0.72f; hy[6] = top - 0.012f; hl[7] = 0.45f; hy[7] = top - 0.004f; }
            hl[8] = 0f; hy[8] = top;
            var row = new Vector3[C];
            for (int k = 0; k < 9; k++) row[k] = X(c, ua, ub, u, hy[k], hl[k] * rx);
            for (int k = 1; k <= 7; k++) row[16 - k] = X(c, ua, ub, u, hy[k], -hl[k] * rx);
            rows[s] = row;
        }
        BeginPart(); AGrid(rows, true, true, true); EndPart(0.03f, 999f);
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            int kb = sgn > 0 ? 5 : 11, kr = sgn > 0 ? 7 : 9; var off = ub * (0.015f * sgn);
            var win = new List<Vector3>(); foreach (int s in cab) win.Add(rows[s][kb] + off); for (int i = cab.Count - 1; i >= 0; i--) win.Add(rows[cab[i]][kr] + off); Poly(win);
            foreach (int s in new[] { cab[0] + 1, cab[cab.Count / 2], cab[cab.Count - 1] - 1 }) L(rows[s][kb] + off, rows[s][kr] + off);   // pillars
            foreach (float x in new[] { -0.85f, 0f, 0.95f }) L(X(c, ua, ub, x, 0.38f, sgn * 0.885f), X(c, ua, ub, x, BELT, sgn * 0.885f));   // door seams
            L(X(c, ua, ub, -0.35f, 0.72f, sgn * 0.89f), X(c, ua, ub, -0.1f, 0.72f, sgn * 0.89f)); L(X(c, ua, ub, 0.2f, 0.72f, sgn * 0.89f), X(c, ua, ub, 0.45f, 0.72f, sgn * 0.89f));   // handles
            PCone(X(c, ua, ub, 0.95f, 0.98f, sgn * 0.88f), X(c, ua, ub, 0.9f, 1.02f, sgn * 1.1f), 0.04f, 0.045f, 6);                 // mirror
            foreach (float x in new[] { 1.3f, -1.35f })                                                                              // wheels
            {
                var wc = X(c, ua, ub, x, 0.33f, sgn * 0.9f);
                PCone(wc - ub * (0.2f * sgn), wc, 0.34f, 0.34f, 20);
                foreach (float r in new[] { 0.34f, 0.25f, 0.12f, 0.04f }) Circle(wc + ub * (0.01f * sgn), ua, Vector3.up, r, 20);
                for (int k = 0; k < 5; k++) { float a = 2f * Mathf.PI * k / 5f + 0.3f; L(wc + ub * (0.01f * sgn), wc + ub * (0.01f * sgn) + ua * (Mathf.Cos(a) * 0.24f) + Vector3.up * (Mathf.Sin(a) * 0.24f)); }
            }
            Circle(X(c, ua, ub, 2.12f, 0.62f, sgn * 0.55f), ub * sgn, Vector3.up, 0.12f, 12); Circle(X(c, ua, ub, -2.12f, 0.7f, sgn * 0.55f), ub * sgn, Vector3.up, 0.09f, 12);   // head and tail lamps
        }
        for (int e = 0; e < 2; e++) for (int i = 0; i < 8; i++) { float y = e == 0 ? 0.42f : 0.55f, z0 = Mathf.Lerp(-0.55f + e * 0.05f, 0.55f - e * 0.05f, i / 8f), z1 = Mathf.Lerp(-0.55f + e * 0.05f, 0.55f - e * 0.05f, (i + 1) / 8f); L(X(c, ua, ub, 2.14f, y, z0), X(c, ua, ub, 2.14f, y, z1)); }   // grille
    }
    static float Interp(float x, float[] xs, float[] ys)
    {
        if (x <= xs[0]) return ys[0]; if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
        for (int i = 0; i < xs.Length - 1; i++) if (x <= xs[i + 1]) return Mathf.Lerp(ys[i], ys[i + 1], (x - xs[i]) / (xs[i + 1] - xs[i]));
        return ys[ys.Length - 1];
    }

    // ---- bench (bsd = which side the back is on), bin, traffic light ----
    void Bench(Vector3 bp, Vector3 ua, Vector3 ub, float bsd)
    {
        var u2 = ub * bsd;
        PBoxPart(bp, ua, u2, 1.6f, 0.45f, 0.42f, 0.48f);                                                             // the seat
        foreach (float x in new[] { -0.7f, 0.7f }) PBoxPart(bp + ua * x, ua, u2, 0.06f, 0.4f, 0f, 0.42f);          // the legs
        foreach (float y in new[] { 0.7f, 0.88f })                                                                  // two slats, bowed backwards
        {
            var rows = new Vector3[2][];
            for (int r = 0; r < 2; r++) { rows[r] = new Vector3[10]; for (int i = 0; i < 10; i++) { float x = Mathf.Lerp(-0.8f, 0.8f, i / 9f); rows[r][i] = X(bp, ua, u2, x, y + (r == 0 ? -0.06f : 0.06f), -0.22f - 0.15f * (1f - 4f * (x / 1.6f) * (x / 1.6f))); } }
            BeginPart(); AGrid(rows, false, false, false); EndPart(0f, 999f);
        }
        foreach (float x in new[] { -0.7f, 0.7f }) L(X(bp, ua, u2, x, 0.45f, -0.2f), X(bp, ua, u2, x, 0.95f, -0.26f));
    }
    void Bin(Vector3 bn, float s = 1f)
    {
        PCone(bn, bn + Vector3.up * 0.9f * s, 0.25f * s, 0.25f * s, 16); PCone(bn + Vector3.up * 0.88f * s, bn + Vector3.up * 0.96f * s, 0.28f * s, 0.27f * s, 16);
    }
    void TrafficLight(Vector3 c, Vector3 toward)
    {
        var tg = Vector3.Cross(Vector3.up, toward);
        PCone(c, c + Vector3.up * 3.55f, 0.07f, 0.06f, 10);
        PBlob(c + Vector3.up * 4.05f, right, fwd, 0.2f, 0.5f, 0.2f, 0f, 0f, 12, 8);
        for (int q = 0; q < 3; q++) Circle(c + toward * 0.17f + Vector3.up * (4.33f - 0.28f * q), tg, Vector3.up, 0.08f, 10);
    }

    // ---- stroller: a chassis with big rear and small front wheels, a seat tub, a ribbed canopy with a visor, a basket, a footrest, harness, brake bar, push handle ----
    void Stroller(Vector3 b)
    {
        Vector3 ua = right, ub = fwd; Vector3 F(float x, float y, float z) => X(b, ua, ub, x, y, z);
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            PCone(F(x, 0.36f, 0.42f), F(x, 0.36f, -0.45f), 0.014f, 0.014f, 6); PCone(F(x, 0.36f, 0.42f), F(x, 0.12f, 0.5f), 0.012f, 0.012f, 6); PCone(F(x, 0.36f, -0.4f), F(x, 0.17f, -0.5f), 0.012f, 0.012f, 6);
        }
        PCone(F(-0.27f, 0.2f, -0.2f), F(0.27f, 0.34f, 0.1f), 0.01f, 0.01f, 6); PCone(F(0.27f, 0.2f, -0.2f), F(-0.27f, 0.34f, 0.1f), 0.01f, 0.01f, 6);
        foreach (float x in new[] { -0.31f, 0.31f })                                                                      // wheels: tyre, rim, spokes, hub cap, tread, mudguard (rear)
            foreach (var w in new[] { new Vector3(-0.5f, 0.18f, 0.18f), new Vector3(0.5f, 0.1f, 0.1f) })
            {
                float wz = w.x, wy = w.y, r = w.z; int sg = x > 0 ? 1 : -1; var wc = F(x, wy, wz);
                PCone(wc - ua * (0.025f * sg), wc + ua * (0.025f * sg), r, r, 18);
                foreach (float rr in new[] { r, r * 0.7f, r * 0.15f }) Circle(wc + ua * (0.026f * sg), Vector3.up, fwd, rr, 18);
                for (int k = 0; k < 6; k++) { float a = Mathf.PI * 2f * k / 6f; L(wc + ua * (0.026f * sg), wc + ua * (0.026f * sg) + Vector3.up * (Mathf.Cos(a) * r * 0.7f) + fwd * (Mathf.Sin(a) * r * 0.7f)); }
                PCone(wc + ua * (0.025f * sg), wc + ua * (0.045f * sg), r * 0.22f, r * 0.22f, 8);
                foreach (float dx in new[] { -0.012f, 0.012f }) Circle(F(x + dx, wy, wz), Vector3.up, fwd, r * 1.02f, 18);
                if (wz < 0f) { var mg = new List<Vector3>(); for (int i = 0; i < 9; i++) { float a = Mathf.Lerp(0.2f, 2.9f, i / 8f); mg.Add(F(x + 0.03f * sg, 0.18f + 0.2f * Mathf.Sin(a), -0.5f + 0.2f * Mathf.Cos(a))); } Poly(mg, false); }
            }
        // seat tub: low at the front, tall at the back
        var rows = new Vector3[9][]; float[] topY = { 0.52f, 0.54f, 0.58f, 0.62f, 0.66f, 0.7f, 0.74f, 0.78f, 0.8f };
        for (int s = 0; s < 9; s++)
        {
            float z = Mathf.Lerp(0.38f, -0.38f, s / 8f), w = 0.27f - 0.01f * Mathf.Abs(z), bot = 0.4f, top = topY[s]; rows[s] = new Vector3[16];
            for (int k = 0; k < 16; k++) { float a = 2f * Mathf.PI * k / 16f, cx = Mathf.Cos(a), cy = Mathf.Sin(a); rows[s][k] = F(Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 0.55f) * w, (bot + top) / 2f + Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), 0.7f) * (top - bot) / 2f, z); }
        }
        BeginPart(); AGrid(rows, true, true, true); EndPart(0.02f, 999f);
        // rim, cushion line, harness, buckle
        foreach (float x in new[] { -0.262f, 0.262f }) for (int s = 0; s < 8; s++) { float z0 = Mathf.Lerp(0.38f, -0.38f, s / 8f), z1 = Mathf.Lerp(0.38f, -0.38f, (s + 1) / 8f); L(F(x, topY[s] + 0.012f, z0), F(x, topY[s + 1] + 0.012f, z1)); }
        for (int i = 0; i < 8; i++) { float a0 = Mathf.PI * (1f - i / 8f), a1 = Mathf.PI * (1f - (i + 1) / 8f); L(F(0.262f * Mathf.Cos(a0), 0.532f, 0.38f + 0.06f * Mathf.Sin(a0)), F(0.262f * Mathf.Cos(a1), 0.532f, 0.38f + 0.06f * Mathf.Sin(a1))); }
        foreach (float x in new[] { -0.2f, 0.2f }) for (int s = 0; s < 8; s++) L(F(x, topY[s] + 0.016f, Mathf.Lerp(0.38f, -0.38f, s / 8f)), F(x, topY[s + 1] + 0.016f, Mathf.Lerp(0.38f, -0.38f, (s + 1) / 8f)));
        foreach (float x in new[] { -0.12f, 0.12f }) { L(F(x, 0.8f, -0.37f), F(x * 0.2f, 0.64f, -0.08f)); L(F(x * 0.2f, 0.64f, -0.08f), F(x * 0.05f, 0.58f, 0f)); }
        L(F(-0.24f, 0.62f, -0.12f), F(0.24f, 0.62f, -0.12f)); Circle(F(0f, 0.585f, 0f), right, fwd, 0.03f, 8);
        // footrest
        var fr = new Vector3[2][]; for (int r = 0; r < 2; r++) { fr[r] = new Vector3[6]; for (int i = 0; i < 6; i++) fr[r][i] = F(Mathf.Lerp(-0.24f, 0.24f, i / 5f), r == 0 ? 0.3f : 0.22f, r == 0 ? 0.46f : 0.56f); }
        BeginPart(); AGrid(fr, false, false, false); EndPart(0f, 999f);
        // basket under the seat
        var bk = new Vector3[5][]; for (int s = 0; s < 5; s++) { bk[s] = new Vector3[9]; for (int i = 0; i < 9; i++) { float a = Mathf.PI * (1f + i / 8f); bk[s][i] = F(0.27f * Mathf.Cos(a), 0.2f + 0.07f * Mathf.Sin(a), Mathf.Lerp(-0.25f, 0.25f, s / 4f)); } }
        BeginPart(); AGrid(bk, false, false, false); EndPart(0f, 999f);
        // canopy: a half-dome over the back half, ribs, a visor and a pull handle
        var cn = new Vector3[7][];
        for (int s = 0; s < 7; s++) { float z = Mathf.Lerp(-0.38f, 0.02f, s / 6f), t = (z + 0.38f) / 0.4f, r = 0.29f * (1f - 0.35f * t * t); cn[s] = new Vector3[13]; for (int i = 0; i < 13; i++) { float a = Mathf.PI * i / 12f; cn[s][i] = F(Mathf.Cos(a) * r, 0.74f + Mathf.Sin(a) * r * (1f - 0.45f * t), z); } }
        BeginPart(); AGrid(cn, false, false, false); EndPart(0f, 999f);
        foreach (float z in new[] { -0.36f, -0.3f, -0.22f, -0.12f, -0.04f })
        {
            float t = (z + 0.38f) / 0.4f, r = 0.29f * (1f - 0.35f * t * t) * 1.006f; var rib = new List<Vector3>();
            for (int i = 0; i < 13; i++) { float a = Mathf.PI * i / 12f; rib.Add(F(Mathf.Cos(a) * r, 0.74f + Mathf.Sin(a) * r * (1f - 0.45f * t) * 1.006f, z)); }
            Poly(rib, false);
        }
        var vs = new Vector3[2][]; for (int r = 0; r < 2; r++) { vs[r] = new Vector3[11]; for (int i = 0; i < 11; i++) { float a = Mathf.PI * i / 10f; vs[r][i] = r == 0 ? F(0.29f * Mathf.Cos(a), 0.74f + 0.2f * Mathf.Sin(a), 0.02f) : F(0.27f * Mathf.Cos(a), 0.74f + 0.16f * Mathf.Sin(a), 0.15f); } }
        BeginPart(); AGrid(vs, false, false, false); EndPart(0f, 999f);
        PCone(F(-0.06f, 0.945f, 0.07f), F(0.06f, 0.945f, 0.07f), 0.012f, 0.012f, 6);
        // joints on the rails, axles, foot brake bar and pedal
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            foreach (float z in new[] { 0.1f, -0.28f }) PCone(F(x - 0.025f, 0.36f, z), F(x + 0.025f, 0.36f, z), 0.025f, 0.025f, 8);
            PCone(F(x, 0.45f, -0.4f), F(x, 0.55f, -0.4f), 0.022f, 0.022f, 8);
        }
        PCone(F(-0.31f, 0.18f, -0.5f), F(0.31f, 0.18f, -0.5f), 0.012f, 0.012f, 6); PCone(F(-0.31f, 0.1f, 0.5f), F(0.31f, 0.1f, 0.5f), 0.01f, 0.01f, 6);
        PCone(F(-0.2f, 0.24f, -0.56f), F(0.2f, 0.24f, -0.56f), 0.014f, 0.014f, 6);
        var pd = new Vector3[2][]; for (int r = 0; r < 2; r++) { pd[r] = new Vector3[2]; for (int i = 0; i < 2; i++) pd[r][i] = F(i == 0 ? -0.05f : 0.05f, r == 0 ? 0.24f : 0.2f, r == 0 ? -0.56f : -0.64f); }
        BeginPart(); AGrid(pd, false, false, false); EndPart(0f, 999f);
        // the push handle: two uprights bending back to a grip bar, ridged grips and height joints
        foreach (float x in new[] { -0.25f, 0.25f })
        {
            Vector3[] hp = { F(x, 0.5f, -0.4f), F(x, 0.75f, -0.5f), F(x, 0.98f, -0.62f), F(x, 1.08f, -0.78f) };
            for (int i = 0; i < 3; i++) PCone(hp[i], hp[i + 1], 0.014f, 0.014f, 6);
            PCone(F(x, 0.82f, -0.55f), F(x, 0.97f, -0.61f), 0.025f, 0.025f, 8);
            for (int i = 0; i < 5; i++) { float t = Mathf.Lerp(0.1f, 0.9f, i / 4f); Circle(F(x, 0.82f + 0.15f * t, -0.55f - 0.06f * t), right, fwd, 0.027f, 8); }
            PCone(F(x - 0.02f, 0.64f, -0.46f), F(x + 0.02f, 0.64f, -0.46f), 0.03f, 0.03f, 8);
        }
        PCone(F(-0.25f, 1.08f, -0.78f), F(0.25f, 1.08f, -0.78f), 0.02f, 0.02f, 8);
        for (int i = 0; i < 8; i++) { float a0 = Mathf.PI * (1f - i / 8f), a1 = Mathf.PI * (1f - (i + 1) / 8f); L(F(0.27f * Mathf.Cos(a0), 0.58f, 0.4f + 0.1f * Mathf.Sin(a0)), F(0.27f * Mathf.Cos(a1), 0.58f, 0.4f + 0.1f * Mathf.Sin(a1))); }   // bumper bar
    }

    // ---- litter: real small objects lying on the ground (outline over solid): cans, bottles, cups, crumpled paper, newspaper, bags. p is on the ground, r picks the kind, k scales it ----
    void Litter(Vector3 p, float r, float k)
    {
        float ang = r * 977f; var ua = right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang); var ub = Vector3.Cross(Vector3.up, ua);
        if (r < 0.22f) PCone(p + Vector3.up * 0.033f * k - ua * 0.06f * k, p + Vector3.up * 0.033f * k + ua * 0.06f * k, 0.033f * k, 0.033f * k, 8);                           // a can on its side
        else if (r < 0.45f) PBlob(p + Vector3.up * 0.05f * k, right, fwd, 0.09f * k, 0.065f * k, 0.08f * k, r * 100f, 0.3f, 8, 5);                                          // crumpled paper
        else if (r < 0.6f) PCone(p + Vector3.up * 0.04f * k - ua * 0.04f * k, p + Vector3.up * 0.035f * k + ua * 0.04f * k, 0.03f * k, 0.04f * k, 8);                    // a paper cup on its side
        else if (r < 0.75f) { PCone(p + Vector3.up * 0.03f * k - ua * 0.1f * k, p + Vector3.up * 0.03f * k + ua * 0.05f * k, 0.03f * k, 0.03f * k, 8); PCone(p + Vector3.up * 0.03f * k + ua * 0.05f * k, p + Vector3.up * 0.025f * k + ua * 0.1f * k, 0.03f * k, 0.012f * k, 8); }   // a bottle
        else if (r < 0.9f) PBoxPart(p, ua, ub, 0.16f * k, 0.11f * k, 0f, 0.008f * k);                                                                                      // a sheet of newspaper
        else PBlob(p + Vector3.up * 0.02f * k, ua, ub, 0.1f * k, 0.03f * k, 0.07f * k, r * 100f, 0.35f, 8, 4);                                                           // a plastic bag
    }
}
