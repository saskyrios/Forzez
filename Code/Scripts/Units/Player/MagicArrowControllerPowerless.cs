using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class MagicArrowControllerPowerless : MonoBehaviour {
    
    [Tooltip("Energy cost of one cast")]
    public float castEnergyCost = 10f;
    
    [Tooltip("Start speed of shell")]
    public float shellsStartSpeed = 10f;
    
    [Tooltip("Magic arrow shell prefab")]
    public GameObject shellPrefab;
    
    [Tooltip("Aim line renderer reference")]
    public LineRenderer aimLine;

    // END INSPECTOR DECLARATION
    
    private PlayerMovable _playerMovable;
    private PlayerCore _playerCore;
    private Vector2 _inputVector;
    [CanBeNull] private Shell _preparedShell;
    
    private Vector2 CalculateTrajectory(Vector2 startPosition, Vector2 startVelocity, float gravity, float time) {
        var x = startPosition.x + startVelocity.x * time;
        var y = startPosition.y + startVelocity.y * time + gravity * Mathf.Pow(time, 2) / 2;
        return new Vector2(x, y);
    }

    private Vector2 GetAbsoluteAimDirection() {
        var direction = _inputVector;
        if (Math.Abs(direction.magnitude) < Config.FloatPrecision) {
            direction = Vector2.right * _playerMovable.Direction;
        }
        return direction.normalized;
    }

    private void PrepareShell() {
        if (_playerCore.CurrentEnergy < castEnergyCost) {
            return;
        }
        _playerCore.CurrentEnergy -= castEnergyCost;
        
        var shellObject = Instantiate(shellPrefab, transform.position, Quaternion.identity);
        shellObject.transform.parent = transform;
        _preparedShell = shellObject.GetComponent<Shell>();
        _preparedShell.gameObject.SetActive(false);
        UpdateAimLine();
    }
    
    private void Shoot() {
        if (!_preparedShell) {
            return;
        }

        aimLine.enabled = false;

        var direction = GetAbsoluteAimDirection();
        
        _preparedShell.gameObject.SetActive(true);
        _preparedShell.transform.parent = null;
        _preparedShell.Throw(direction * shellsStartSpeed);

        _preparedShell = null;
    }

    private void UpdateAimLine() {
        if (!_preparedShell) {
            return;
        }

        var direction = GetAbsoluteAimDirection();

        const float segmentSize = 5f;
        const float aimLineLength = 100f;
        
        var segmentsCount = (int)Math.Round(aimLineLength / segmentSize);
        var segmentTime = segmentSize / shellsStartSpeed;

        var startPoint = transform.position;
        var points = new List<Vector3>(new []{Vector3.zero});
        var prevPoint = startPoint;
        for (var i = 1; i < segmentsCount; ++i) {
            var point = CalculateTrajectory(startPoint,
                direction * shellsStartSpeed,
                _preparedShell.GravityFactor,
                i * segmentTime
            );
            
            // Debug.DrawLine(transform.TransformPoint(points.Last()), transform.TransformPoint(absolutePoint), Color.red, 0.02f);
            RaycastHit2D hit =  Physics2D.Linecast(prevPoint, point, Config.ObstacleLayerMask);
            if (hit) {
                points.Add(transform.InverseTransformPoint(hit.point));
                break;
            }

            prevPoint = point;
            points.Add(transform.InverseTransformPoint(point));
        }
        
        aimLine.positionCount = points.Count;
        aimLine.SetPositions(points.ToArray());
        aimLine.enabled = true;
    }

    private void FixedUpdate() {
        UpdateAimLine();
    }

    public void Initialize() {
        _playerMovable = GameManager.Instance.player.Movable;
        _playerCore = GameManager.Instance.player.Core;
        GameManager.Instance.playerInputActions["Aim"].performed += context => {
            if (!enabled) {
                return;
            }
            
            _inputVector = context.ReadValue<Vector2>();
        };
        
        _playerMovable.DirectionChanged += value => {
            if (!enabled) {
                return;
            }
            
            UpdateAimLine();
        };

        GameManager.Instance.playerInputActions["Skill"].started += context => {
            if (!enabled) {
                return;
            }
            
            PrepareShell();
        };
        GameManager.Instance.playerInputActions["Skill"].canceled += context => {
            if (!enabled) {
                return;
            }
            
            Shoot();
        };
    }
}