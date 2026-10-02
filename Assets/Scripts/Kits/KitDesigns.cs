using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildCrew.Kits
{
    /// <summary>
    /// Procedural designs of the built-in kits. The editor KitGenerator writes
    /// them to StreamingAssets/Kits/*.json; KitLoader falls back to them if a
    /// file is missing. Only Vector3/Mathf are used, so the offline
    /// Tools~/kit-gen harness can run this file too.
    ///
    /// Frame: build-site local, x right, y up, front of the building at -z.
    /// Linear parts run along local x; Euler (0,0,90) stands one up,
    /// (0,90,0) turns it to run along z.
    /// </summary>
    public static class KitDesigns
    {
        public const string GardenShedFile = "garden_shed.json";
        public const string CottageFile = "cottage.json";

        public static KitDefinition ByFileName(string fileName)
        {
            switch (fileName)
            {
                case GardenShedFile: return GardenShed();
                case CottageFile: return Cottage();
                default: return null;
            }
        }

        public static IEnumerable<KeyValuePair<string, KitDefinition>> All()
        {
            yield return new KeyValuePair<string, KitDefinition>(GardenShedFile, GardenShed());
            yield return new KeyValuePair<string, KitDefinition>(CottageFile, Cottage());
        }

        static readonly Vector3 Upright = new Vector3(0f, 0f, 90f);
        static readonly Vector3 AlongZ = new Vector3(0f, 90f, 0f);

        // =========================================================== garden shed

        /// <summary>
        /// 3 x 2.5 m footprint, 2.4 m high. Six posts (corners + door posts),
        /// 0.4 m wide boards on the outside, three beams across the top, three
        /// corrugated sheets, a door.
        /// </summary>
        public static KitDefinition GardenShed()
        {
            var b = new Builder();
            const float w = 3f, d = 2.5f, h = 2.4f;
            const float post = 0.12f, board = 0.4f, boardT = 0.03f;
            float px = w * 0.5f - post * 0.5f;   // 1.44
            float pz = d * 0.5f - post * 0.5f;   // 1.19
            const float doorHalf = 0.5f;         // opening between the door posts' inner faces
            float dpx = doorHalf + post * 0.5f;  // door post centers

            // ---- stage 0: posts (stand on the ground)
            var postSize = new Vector3(h, post, post);
            b.Add("post_fl", "post", postSize, new Vector3(-px, h * 0.5f, -pz), Upright, 0, "nails");
            b.Add("post_fr", "post", postSize, new Vector3(px, h * 0.5f, -pz), Upright, 0, "nails");
            b.Add("post_bl", "post", postSize, new Vector3(-px, h * 0.5f, pz), Upright, 0, "nails");
            b.Add("post_br", "post", postSize, new Vector3(px, h * 0.5f, pz), Upright, 0, "nails");
            b.Add("post_dl", "post", postSize, new Vector3(-dpx, h * 0.5f, -pz), Upright, 0, "nails");
            b.Add("post_dr", "post", postSize, new Vector3(dpx, h * 0.5f, -pz), Upright, 0, "nails");

            // ---- stage 1: wall boards on the outer faces of the posts
            int rows = Mathf.RoundToInt(h / board); // 6
            float doorTop = 2.0f;
            float faceZ = d * 0.5f + boardT * 0.5f;
            float faceX = w * 0.5f + boardT * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                float y = board * 0.5f + r * board;
                b.Add($"board_back_{r}", "plank", new Vector3(w, board, boardT), new Vector3(0f, y, faceZ), Vector3.zero, 1, "nails",
                    "post_bl", "post_br");
                b.Add($"board_left_{r}", "plank", new Vector3(d, board, boardT), new Vector3(-faceX, y, 0f), AlongZ, 1, "nails",
                    "post_fl", "post_bl");
                b.Add($"board_right_{r}", "plank", new Vector3(d, board, boardT), new Vector3(faceX, y, 0f), AlongZ, 1, "nails",
                    "post_fr", "post_br");
                if (y < doorTop)
                {
                    // Split around the door opening: 1.0 m pieces, two per 2 m plank.
                    float len = w * 0.5f - doorHalf;
                    float cx = doorHalf + len * 0.5f;
                    b.Add($"board_front_l_{r}", "plank", new Vector3(len, board, boardT), new Vector3(-cx, y, -faceZ), Vector3.zero, 1, "nails",
                        "post_fl", "post_dl");
                    b.Add($"board_front_r_{r}", "plank", new Vector3(len, board, boardT), new Vector3(cx, y, -faceZ), Vector3.zero, 1, "nails",
                        "post_fr", "post_dr");
                }
                else
                {
                    b.Add($"board_front_{r}", "plank", new Vector3(w, board, boardT), new Vector3(0f, y, -faceZ), Vector3.zero, 1, "nails",
                        "post_fl", "post_fr", "post_dl", "post_dr");
                }
            }
            string topFront = $"board_front_{rows - 1}", topBack = $"board_back_{rows - 1}";

            // ---- stage 2: roof beams front-to-back with a little overhang (cut 3 m beams to 2.9 m)
            const float beamH = 0.2f, beamW = 0.1f;
            var beamSize = new Vector3(2.9f, beamH, beamW);
            float beamY = h + beamH * 0.5f;
            b.Add("beam_l", "beam", beamSize, new Vector3(-px, beamY, 0f), AlongZ, 2, "nails", "post_fl", "post_bl");
            b.Add("beam_m", "beam", beamSize, new Vector3(0f, beamY, 0f), AlongZ, 2, "nails", topFront, topBack);
            b.Add("beam_r", "beam", beamSize, new Vector3(px, beamY, 0f), AlongZ, 2, "nails", "post_fr", "post_br");

            // ---- stage 3: corrugated sheets (3.0 long front-to-back, 1.2 wide)
            const float sheetT = 0.03f;
            var sheetSize = new Vector3(3f, sheetT, 1.2f);
            float sheetY = h + beamH + sheetT * 0.5f;
            b.Add("sheet_l", "metalSheet", sheetSize, new Vector3(-1.2f, sheetY, 0f), AlongZ, 3, "screws", "beam_l");
            b.Add("sheet_m", "metalSheet", sheetSize, new Vector3(0f, sheetY, 0f), AlongZ, 3, "screws", "beam_m");
            b.Add("sheet_r", "metalSheet", sheetSize, new Vector3(1.2f, sheetY, 0f), AlongZ, 3, "screws", "beam_r");

            // ---- stage 4: door between the door posts
            b.Add("door", "door", new Vector3(doorHalf * 2f - 0.02f, doorTop - 0.02f, 0.04f), new Vector3(0f, doorTop * 0.5f + 0.01f, -pz),
                Vector3.zero, 4, "screws", "post_dl", "post_dr");

            return new KitDefinition
            {
                id = "garden_shed",
                name = "Garden Shed",
                description = "A 3 x 2.5 m timber shed with a tin roof. Cut the boards, mind the door.",
                roundTime = 240f,
                finalTest = "wind",
                stages = new[] { "Posts", "Wall planks", "Roof beams", "Corrugated roof", "Door" },
                slots = b.ToArray(),
            };
        }

        // =============================================================== cottage

        /// <summary>
        /// 6 x 5 m brick cottage, ridge ~5.2 m. Foundation stones, walls of
        /// brick stacks (one stack = 10 bricks = 1.5 x 0.4 m wall section) with
        /// a door and four windows, frames + panes + door, rafters and ridge,
        /// roof tile panels, and a chimney of crossed brick stacks.
        /// </summary>
        public static KitDefinition Cottage()
        {
            var b = new Builder();
            const float w = 6f, d = 5f, wallT = 0.2f;
            const float stoneH = 0.25f;
            const float sec = 1.5f, row = 0.4f;
            const int rows = 6;
            float wallTop = stoneH + rows * row; // 2.65
            float fz = d * 0.5f - wallT * 0.5f;  // 2.4  front/back wall center lines (z = -fz / +fz)
            float sx = w * 0.5f - wallT * 0.5f;  // 2.9  side wall center lines

            // Walls: name, section centers (wall-local u), whether it runs along z, fixed coordinate.
            var walls = new[]
            {
                new Wall("front", 4, false, -fz),
                new Wall("back", 4, false, fz),
                new Wall("left", 3, true, -sx),
                new Wall("right", 3, true, sx),
            };
            // Openings: (wall, section, first row, last row). Door 2.0 m, windows 1.2 m.
            var openings = new List<Opening>
            {
                new Opening("front", 1, 0, 4, true),
                new Opening("front", 3, 2, 4, false),
                new Opening("back", 2, 2, 4, false),
                new Opening("left", 1, 2, 4, false),
                new Opening("right", 1, 2, 4, false),
            };
            bool IsOpen(string wall, int i, int r) =>
                openings.Exists(o => o.Wall == wall && o.Section == i && r >= o.FirstRow && r <= o.LastRow);

            // ---- stage 0: foundation stones
            var stoneSize = new Vector3(sec, stoneH, 0.25f);
            foreach (Wall wl in walls)
                for (int i = 0; i < wl.Sections; i++)
                    b.Add($"stone_{wl.Name}_{i}", "stone", stoneSize, wl.Pos(i, stoneH * 0.5f), wl.Euler, 0, "mortar");

            // ---- stage 1: brick stacks
            var stackSize = new Vector3(sec, row, wallT);
            foreach (Wall wl in walls)
            {
                for (int r = 0; r < rows; r++)
                for (int i = 0; i < wl.Sections; i++)
                {
                    if (IsOpen(wl.Name, i, r)) continue;
                    var rests = new List<string>();
                    if (r == 0) rests.Add($"stone_{wl.Name}_{i}");
                    else if (!IsOpen(wl.Name, i, r - 1)) rests.Add(Brick(wl.Name, i, r - 1));
                    else
                    {
                        // Lintel over an opening: carried by its neighbours in the same row.
                        if (i > 0 && !IsOpen(wl.Name, i - 1, r)) rests.Add(Brick(wl.Name, i - 1, r));
                        if (i < wl.Sections - 1 && !IsOpen(wl.Name, i + 1, r)) rests.Add(Brick(wl.Name, i + 1, r));
                    }
                    b.Add(Brick(wl.Name, i, r), "brickStack", stackSize, wl.Pos(i, stoneH + row * (r + 0.5f)), wl.Euler, 1, "mortar",
                        rests.ToArray());
                }
            }

            // ---- stage 2: window frames + panes, door
            foreach (Opening o in openings)
            {
                Wall wl = Array.Find(walls, x => x.Name == o.Wall);
                float bottom = stoneH + o.FirstRow * row;
                float height = (o.LastRow - o.FirstRow + 1) * row;
                Vector3 c = wl.Pos(o.Section, bottom + height * 0.5f);
                if (o.IsDoor)
                {
                    b.Add($"door_{o.Wall}", "door", new Vector3(sec - 0.02f, height - 0.02f, 0.06f), c + new Vector3(0f, 0.01f, 0f), wl.Euler, 2,
                        "screws", $"stone_{o.Wall}_{o.Section}");
                }
                else
                {
                    string frame = $"frame_{o.Wall}";
                    b.Add(frame, "windowFrame", new Vector3(sec, height, 0.1f), c, wl.Euler, 2, "nails",
                        Brick(o.Wall, o.Section, o.FirstRow - 1));
                    // Pane fits the frame's inner opening (1.0 x 0.8): cut from a 1 x 1 standard pane.
                    b.Add($"pane_{o.Wall}", "pane", new Vector3(1.0f, 0.8f, 0.006f), c, wl.Euler, 2, "nails", frame);
                }
            }

            // ---- stage 3: rafters (4 m beams, no cut) butting against a vertical ridge beam
            const float beamH = 0.2f, beamW = 0.1f;
            const float rafterLen = 4f, overhang = 0.5f;
            float run = fz + overhang;                                  // 2.9 horizontal length of a rafter
            float rise = Mathf.Sqrt(rafterLen * rafterLen - run * run); // ~2.76
            float a = Mathf.Atan2(rise, run);
            float s = Mathf.Sin(a), co = Mathf.Cos(a);
            // The rafter's upper end stops short of the ridge beam (z = +-beamW/2): its end face is
            // square to the slope, so the lower corner reaches (beamH/2) sin(a) further.
            float shift = beamW * 0.5f + beamH * 0.5f * s + 0.005f;
            // Centerline height at the eave end, chosen so the rafter's underside rests on the
            // outer edge of the wall top (z = +-d/2): vertical underside offset is (beamH/2)/cos(a).
            float eaveToWall = run + shift - d * 0.5f;
            float yb = wallTop - eaveToWall * Mathf.Tan(a) + (beamH * 0.5f) / co;
            float aDeg = a * Mathf.Rad2Deg;
            float[] rafterX = { -2.9f, -1.45f, 0f, 1.45f, 2.9f };
            for (int k = 0; k < rafterX.Length; k++)
            {
                float x = rafterX[k];
                foreach (int side in new[] { -1, 1 }) // -1 = front slope
                {
                    string wallName = side < 0 ? "front" : "back";
                    int section = Mathf.Clamp(Mathf.FloorToInt((x + w * 0.5f) / sec), 0, 3);
                    var center = new Vector3(x, yb + rise * 0.5f, side * (shift + run * 0.5f));
                    b.Add(Rafter(side, k), "beam", new Vector3(rafterLen, beamH, beamW), center,
                        new Vector3(0f, side < 0 ? -90f : 90f, aDeg), 3, "nails", Brick(wallName, section, rows - 1));
                }
            }
            // Ridge beams stand on edge between the rafter tops (two 3 m beams).
            float ridgeY = yb + rise;
            for (int half = 0; half < 2; half++)
            {
                float x0 = half == 0 ? -3f : 0f;
                var rests = new List<string>();
                for (int k = 0; k < rafterX.Length; k++)
                    if (rafterX[k] >= x0 - 0.01f && rafterX[k] <= x0 + 3.01f) { rests.Add(Rafter(-1, k)); rests.Add(Rafter(1, k)); }
                b.Add($"ridge_{half}", "beam", new Vector3(3f, beamH, beamW), new Vector3(x0 + 1.5f, ridgeY, 0f), Vector3.zero, 3, "nails",
                    rests.ToArray());
            }

            // ---- stage 4: roof tile panels (1.5 wide, 1.0 down the slope), 4 x 4 per side
            const float tileT = 0.04f;
            float[] tileX = { -2.25f, -0.75f, 0.75f, 2.25f };
            foreach (int side in new[] { -1, 1 })
            {
                // Unit vectors in the y/z plane: up the slope, and perpendicular to it (out of the roof).
                var up = new Vector3(0f, s, -side * co);
                var normal = new Vector3(0f, co, side * s);
                var eave = new Vector3(0f, yb, side * (run + shift));
                for (int course = 0; course < 4; course++)
                for (int t = 0; t < tileX.Length; t++)
                {
                    var rests = new List<string>();
                    for (int k = 0; k < rafterX.Length; k++)
                        if (Mathf.Abs(rafterX[k] - tileX[t]) <= 0.81f) rests.Add(Rafter(side, k));
                    Vector3 c = eave + up * (course + 0.5f) + normal * (beamH * 0.5f + tileT * 0.5f);
                    c.x = tileX[t];
                    b.Add($"tile_{(side < 0 ? "f" : "b")}_{course}_{t}", "roofTile", new Vector3(1.5f, tileT, 1f), c,
                        new Vector3(side < 0 ? -aDeg : aDeg, 0f, 0f), 4, "nails", rests.ToArray());
                }
            }

            // ---- stage 5: chimney on the right half of the ridge: 4 layers of 2 crossed stacks
            float chimX = 1.5f;
            float baseY = ridgeY + beamH * 0.5f;
            const float off = sec * 0.5f - wallT * 0.5f; // 0.65
            for (int layer = 0; layer < 4; layer++)
            {
                float y = baseY + row * (layer + 0.5f);
                bool alongZ = layer % 2 == 0;
                for (int k = 0; k < 2; k++)
                {
                    float o = k == 0 ? -off : off;
                    Vector3 c = alongZ ? new Vector3(chimX + o, y, 0f) : new Vector3(chimX, y, o);
                    string[] rests = layer == 0 ? new[] { "ridge_1" } : new[] { $"chimney_{layer - 1}_0", $"chimney_{layer - 1}_1" };
                    b.Add($"chimney_{layer}_{k}", "brickStack", stackSize, c, alongZ ? AlongZ : Vector3.zero, 5, "mortar", rests);
                }
            }

            return new KitDefinition
            {
                id = "cottage",
                name = "Cottage",
                description = "A 6 x 5 m brick cottage with a tiled roof and a chimney. Bring friends.",
                roundTime = 480f,
                finalTest = "windRain",
                stages = new[] { "Foundation stones", "Brick walls", "Windows and door", "Roof beams", "Roof tiles", "Chimney" },
                slots = b.ToArray(),
            };
        }

        static string Brick(string wall, int section, int row) => $"brick_{wall}_{row}_{section}";
        static string Rafter(int side, int k) => $"rafter_{(side < 0 ? "f" : "b")}_{k}";

        struct Wall
        {
            public readonly string Name;
            public readonly int Sections;
            readonly bool _alongZ;
            readonly float _fixed;

            public Wall(string name, int sections, bool alongZ, float fixedCoord)
            {
                Name = name;
                Sections = sections;
                _alongZ = alongZ;
                _fixed = fixedCoord;
            }

            public Vector3 Euler => _alongZ ? AlongZ : Vector3.zero;

            /// <summary>Center of section i at height y (sections are 1.5 m, centered on the wall).</summary>
            public Vector3 Pos(int i, float y)
            {
                float u = (i - (Sections - 1) * 0.5f) * 1.5f;
                return _alongZ ? new Vector3(_fixed, y, u) : new Vector3(u, y, _fixed);
            }
        }

        struct Opening
        {
            public readonly string Wall;
            public readonly int Section, FirstRow, LastRow;
            public readonly bool IsDoor;

            public Opening(string wall, int section, int firstRow, int lastRow, bool isDoor)
            {
                Wall = wall;
                Section = section;
                FirstRow = firstRow;
                LastRow = lastRow;
                IsDoor = isDoor;
            }
        }

        class Builder
        {
            readonly List<KitSlot> _slots = new List<KitSlot>();

            public void Add(string id, string type, Vector3 size, Vector3 pos, Vector3 euler, int stage, string fix, params string[] restsOn)
            {
                _slots.Add(new KitSlot
                {
                    id = id,
                    partType = type,
                    size = Round(size),
                    position = Round(pos),
                    rotation = Round(euler),
                    stage = stage,
                    fixMethod = fix,
                    restsOn = restsOn ?? new string[0],
                });
            }

            public KitSlot[] ToArray() => _slots.ToArray();

            static Vector3 Round(Vector3 v) => new Vector3(R(v.x), R(v.y), R(v.z));
            static float R(float f) => (float)Math.Round(f, 4);
        }
    }
}
