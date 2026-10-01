using System;
using SoapCarvers.Core;
using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// Owns the current target shape and its voxelized grid (built lazily on first
    /// access, after the SoapBlock has initialized its grid layout).
    /// </summary>
    public class TargetManager : MonoBehaviour
    {
        [SerializeField] GameSettings settings;
        [SerializeField] SoapBlock soap;

        public event Action TargetChanged;

        TargetShape _shape;
        VoxelGrid _grid;

        public void Configure(GameSettings gameSettings, SoapBlock soapBlock)
        {
            settings = gameSettings;
            soap = soapBlock;
        }

        public SoapBlock Soap
        {
            get
            {
                if (soap == null) soap = FindFirstObjectByType<SoapBlock>();
                return soap;
            }
        }

        public TargetShape Shape
        {
            get
            {
                if (_shape == null)
                    _shape = TargetLibrary.Get(settings != null ? settings.targetShapeName : "Swan");
                return _shape;
            }
        }

        public VoxelGrid Grid
        {
            get
            {
                if (_grid == null && Soap != null) _grid = TargetVoxelizer.Build(Shape, Soap);
                return _grid;
            }
        }

        public void SetTarget(string shapeName)
        {
            _shape = TargetLibrary.Get(shapeName);
            _grid = null;
            TargetChanged?.Invoke();
        }
    }
}
