using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Sorting;
using UnityEngine;

namespace BuildCrew.Round
{
    /// <summary>
    /// One team's patch of the world: pile, sorting pallets, plan table, build
    /// site, final-test camera, blueprint studio and player spawn. The scene
    /// supports any number of these; GameManager runs the round on all of them.
    /// </summary>
    public class TeamArea : MonoBehaviour
    {
        [SerializeField] int teamId;
        [SerializeField] BuildSite site;
        [SerializeField] PileDropper pile;
        [SerializeField] FinalTestDirector finalTest;
        [SerializeField] BlueprintStudio studio;
        [SerializeField] List<SortingZone> zones = new List<SortingZone>();
        [SerializeField] Transform playerSpawn;
        [Tooltip("Where a starter box of nails and one of screws appear each round (by the tool rack).")]
        [SerializeField] Transform supplyPoint;

        public int TeamId => teamId;
        public BuildSite Site => site;
        public PileDropper Pile => pile;
        public FinalTestDirector FinalTest => finalTest;
        public BlueprintStudio Studio => studio;
        public IReadOnlyList<SortingZone> Zones => zones;
        public Transform PlayerSpawn => playerSpawn;
        public Transform SupplyPoint => supplyPoint;

        public void Configure(int team, BuildSite buildSite, PileDropper pileDropper, FinalTestDirector test, BlueprintStudio blueprint,
            List<SortingZone> sortingZones, Transform spawn, Transform supplies)
        {
            teamId = team;
            site = buildSite;
            pile = pileDropper;
            finalTest = test;
            studio = blueprint;
            zones = sortingZones;
            playerSpawn = spawn;
            supplyPoint = supplies;
        }
    }
}
