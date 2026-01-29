using Skia3D.Editor;

namespace Skia3D.Sample.ViewModels;

public sealed class CommandStateViewModel : ViewModelBase
{
    private bool _canExtrudeFaces;
    private bool _canBevelFaces;
    private bool _canInsetFaces;
    private bool _canLoopCutEdgeLoop;
    private bool _canSplitEdge;
    private bool _canBridgeEdges;
    private bool _canBridgeEdgeLoops;
    private bool _canMergeVertices;
    private bool _canDissolveFaces;
    private bool _canDissolveEdge;
    private bool _canCollapseEdge;
    private bool _canCleanupMesh;
    private bool _canSmoothMesh;
    private bool _canSimplifyMesh;
    private bool _canPlanarUv;
    private bool _canBoxUv;
    private bool _canNormalizeUv;
    private bool _canFlipU;
    private bool _canFlipV;
    private bool _canUnwrapUv;
    private bool _canPackUv;
    private bool _canMarkUvSeams;
    private bool _canClearUvSeams;
    private bool _canClearAllUvSeams;
    private bool _canSelectUvIsland;
    private bool _canAssignUvGroup;
    private bool _canClearUvGroup;

    public bool CanExtrudeFaces
    {
        get => _canExtrudeFaces;
        set
        {
            if (_canExtrudeFaces == value)
            {
                return;
            }

            _canExtrudeFaces = value;
            RaisePropertyChanged();
        }
    }

    public bool CanBevelFaces
    {
        get => _canBevelFaces;
        set
        {
            if (_canBevelFaces == value)
            {
                return;
            }

            _canBevelFaces = value;
            RaisePropertyChanged();
        }
    }

    public bool CanInsetFaces
    {
        get => _canInsetFaces;
        set
        {
            if (_canInsetFaces == value)
            {
                return;
            }

            _canInsetFaces = value;
            RaisePropertyChanged();
        }
    }

    public bool CanLoopCutEdgeLoop
    {
        get => _canLoopCutEdgeLoop;
        set
        {
            if (_canLoopCutEdgeLoop == value)
            {
                return;
            }

            _canLoopCutEdgeLoop = value;
            RaisePropertyChanged();
        }
    }

    public bool CanSplitEdge
    {
        get => _canSplitEdge;
        set
        {
            if (_canSplitEdge == value)
            {
                return;
            }

            _canSplitEdge = value;
            RaisePropertyChanged();
        }
    }

    public bool CanBridgeEdges
    {
        get => _canBridgeEdges;
        set
        {
            if (_canBridgeEdges == value)
            {
                return;
            }

            _canBridgeEdges = value;
            RaisePropertyChanged();
        }
    }

    public bool CanBridgeEdgeLoops
    {
        get => _canBridgeEdgeLoops;
        set
        {
            if (_canBridgeEdgeLoops == value)
            {
                return;
            }

            _canBridgeEdgeLoops = value;
            RaisePropertyChanged();
        }
    }

    public bool CanMergeVertices
    {
        get => _canMergeVertices;
        set
        {
            if (_canMergeVertices == value)
            {
                return;
            }

            _canMergeVertices = value;
            RaisePropertyChanged();
        }
    }

    public bool CanDissolveFaces
    {
        get => _canDissolveFaces;
        set
        {
            if (_canDissolveFaces == value)
            {
                return;
            }

            _canDissolveFaces = value;
            RaisePropertyChanged();
        }
    }

    public bool CanDissolveEdge
    {
        get => _canDissolveEdge;
        set
        {
            if (_canDissolveEdge == value)
            {
                return;
            }

            _canDissolveEdge = value;
            RaisePropertyChanged();
        }
    }

    public bool CanCollapseEdge
    {
        get => _canCollapseEdge;
        set
        {
            if (_canCollapseEdge == value)
            {
                return;
            }

            _canCollapseEdge = value;
            RaisePropertyChanged();
        }
    }

    public bool CanCleanupMesh
    {
        get => _canCleanupMesh;
        set
        {
            if (_canCleanupMesh == value)
            {
                return;
            }

            _canCleanupMesh = value;
            RaisePropertyChanged();
        }
    }

    public bool CanSmoothMesh
    {
        get => _canSmoothMesh;
        set
        {
            if (_canSmoothMesh == value)
            {
                return;
            }

            _canSmoothMesh = value;
            RaisePropertyChanged();
        }
    }

    public bool CanSimplifyMesh
    {
        get => _canSimplifyMesh;
        set
        {
            if (_canSimplifyMesh == value)
            {
                return;
            }

            _canSimplifyMesh = value;
            RaisePropertyChanged();
        }
    }

    public bool CanPlanarUv
    {
        get => _canPlanarUv;
        set
        {
            if (_canPlanarUv == value)
            {
                return;
            }

            _canPlanarUv = value;
            RaisePropertyChanged();
        }
    }

    public bool CanBoxUv
    {
        get => _canBoxUv;
        set
        {
            if (_canBoxUv == value)
            {
                return;
            }

            _canBoxUv = value;
            RaisePropertyChanged();
        }
    }

    public bool CanNormalizeUv
    {
        get => _canNormalizeUv;
        set
        {
            if (_canNormalizeUv == value)
            {
                return;
            }

            _canNormalizeUv = value;
            RaisePropertyChanged();
        }
    }

    public bool CanFlipU
    {
        get => _canFlipU;
        set
        {
            if (_canFlipU == value)
            {
                return;
            }

            _canFlipU = value;
            RaisePropertyChanged();
        }
    }

    public bool CanFlipV
    {
        get => _canFlipV;
        set
        {
            if (_canFlipV == value)
            {
                return;
            }

            _canFlipV = value;
            RaisePropertyChanged();
        }
    }

    public bool CanUnwrapUv
    {
        get => _canUnwrapUv;
        set
        {
            if (_canUnwrapUv == value)
            {
                return;
            }

            _canUnwrapUv = value;
            RaisePropertyChanged();
        }
    }

    public bool CanPackUv
    {
        get => _canPackUv;
        set
        {
            if (_canPackUv == value)
            {
                return;
            }

            _canPackUv = value;
            RaisePropertyChanged();
        }
    }

    public bool CanMarkUvSeams
    {
        get => _canMarkUvSeams;
        set
        {
            if (_canMarkUvSeams == value)
            {
                return;
            }

            _canMarkUvSeams = value;
            RaisePropertyChanged();
        }
    }

    public bool CanClearUvSeams
    {
        get => _canClearUvSeams;
        set
        {
            if (_canClearUvSeams == value)
            {
                return;
            }

            _canClearUvSeams = value;
            RaisePropertyChanged();
        }
    }

    public bool CanClearAllUvSeams
    {
        get => _canClearAllUvSeams;
        set
        {
            if (_canClearAllUvSeams == value)
            {
                return;
            }

            _canClearAllUvSeams = value;
            RaisePropertyChanged();
        }
    }

    public bool CanSelectUvIsland
    {
        get => _canSelectUvIsland;
        set
        {
            if (_canSelectUvIsland == value)
            {
                return;
            }

            _canSelectUvIsland = value;
            RaisePropertyChanged();
        }
    }

    public bool CanAssignUvGroup
    {
        get => _canAssignUvGroup;
        set
        {
            if (_canAssignUvGroup == value)
            {
                return;
            }

            _canAssignUvGroup = value;
            RaisePropertyChanged();
        }
    }

    public bool CanClearUvGroup
    {
        get => _canClearUvGroup;
        set
        {
            if (_canClearUvGroup == value)
            {
                return;
            }

            _canClearUvGroup = value;
            RaisePropertyChanged();
        }
    }
}
