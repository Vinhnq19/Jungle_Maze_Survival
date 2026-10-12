using System.Collections.Generic;
using _Codebase_.Configs;
using DG.Tweening;
using Managers;
using Screen;
using UnityEngine;

namespace Map
{
    public class BorderSystem : MonoBehaviour
    {
        [Header("Damage Configuration")]
        [SerializeField] private float _baseDamage = 5f;
        [SerializeField] private float _damageUpdateInterval = 10f;
        [SerializeField] private float _damageRampDivisor = 10f;

        [Header("Border Visual References")]
        [SerializeField] private Transform _leftBorder;
        [SerializeField] private Transform _topBorder;
        [SerializeField] private Transform _rightBorder;
        [SerializeField] private Transform _bottomBorder;
        [SerializeField] private Transform _nextBorderIndicator;

        
        // --- Public Properties ---
        public float DarkAreaDamage { get; private set; }
        public bool IsBorderActivated { get; private set; }
        public bool IsShrinking { get; private set; }

        public float TimeToDamagePlayers { get; private set; } = 0.5f;

        // --- Private State ---
        private MapConfig _mapConfig;
        private int _currentWidth;
        private int _shrinkCount;
        private bool _gameStarted;
        private float _timer;
        private float _timeRemainingInMap;
        private float _damageUpdateTimer;
        private float _initialLineScale;

        private Vector2 _mapOrigin; 
        private float _mapWorldWidth; 
        private float _mapWorldHeight;
        private float _borderHalfWidthX;
        
        #region Unity Lifecycle

        private void Awake()
        {
            if (_nextBorderIndicator != null && _nextBorderIndicator.childCount > 0)
            {
                _initialLineScale = _nextBorderIndicator.GetChild(0).localScale.x;
            }
        }

        // private void Start()
        // {
        //     _mapConfig = MapManager.I.Map.mapConfig;
        //     if (_mapConfig == null)
        //     {
        //         Debug.LogError("MapConfig is NOT SET on the Map prefab!", this);
        //         this.enabled = false;
        //         return;
        //     }
        //
        //     var groundTilemap = MapManager.I.Map.ground;
        //     if (groundTilemap != null)
        //     {
        //         _mapOrigin = groundTilemap.transform.localPosition + (Vector3)groundTilemap.cellBounds.min;
        //         _mapWorldWidth = groundTilemap.cellBounds.size.x;
        //         _mapWorldHeight = groundTilemap.cellBounds.size.y;
        //
        //         Debug.Log($"Map Info Initialized: Origin={_mapOrigin}, Size={_mapWorldWidth}x{_mapWorldHeight}");
        //     }
        //     else
        //     {
        //         Debug.LogError("Ground Tilemap not found!", this);
        //         this.enabled = false;
        //         return;
        //     }
        //     
        //     _currentWidth = _mapConfig.InitialWidth;
        //     _timeRemainingInMap = _mapConfig.TotalMapTime;
        //     _minSizeForCurrentMap = _mapConfig.IsLargeMap ? _minSizeBorderLarge : _minSizeBorderSmall;
        //     DarkAreaDamage = _baseDamage;
        //
        //     if (_nextBorderIndicator != null) _nextBorderIndicator.gameObject.SetActive(false);
        //     
        //     
        // }
        
        public void Initialize()
        {
             _mapConfig = MapManager.I.Map.mapConfig;
            if (_mapConfig == null)
            {
                Debug.LogError("MapConfig is NOT SET on the Map prefab!", this);
                this.enabled = false;
                return;
            }

            _mapWorldWidth = _mapConfig.mapSize;
            _mapWorldHeight = _mapConfig.mapSize;

            _mapOrigin = Vector2.zero; 
    
            Debug.Log($"BorderSystem Initialized with MapConfig: Map Origin={_mapOrigin}, Size={_mapWorldWidth}x{_mapWorldHeight}");

            if (_leftBorder != null && _leftBorder.GetComponent<Collider2D>() != null)
            {
                _borderHalfWidthX = _leftBorder.GetComponent<Collider2D>().bounds.extents.x;
                Debug.Log($"Border object half-width calculated: {_borderHalfWidthX}");
            }
            else
            {
                Debug.LogError("Đối tượng Left Border không có Collider2D! Không thể tính toán chiều rộng động.", this);
            }

            _currentWidth = _mapConfig.InitialWidth;
            _timeRemainingInMap = _mapConfig.TotalMapTime;
            _shrinkCount = 0;
            DarkAreaDamage = _baseDamage;

            if (_nextBorderIndicator != null) _nextBorderIndicator.gameObject.SetActive(false);

            Debug.Log("Border System has been successfully initialized.");
        }

        private void Update()
        {
            if (!_gameStarted || _mapConfig == null) return;

            _timer += Time.deltaTime;
            _timeRemainingInMap -= Time.deltaTime;

            UpdateBorderState();
            UpdateDamage();
            UpdateUI();
        }

        #endregion

        #region Core Logic - REFACTORED

        /// <summary>
        /// Manages the state of the border (waiting to start, resting, shrinking).
        /// This is the core logic that fixes the indicator bug.
        /// </summary>
        private void UpdateBorderState()
        {
            // 1. Wait for the initial border activation
            if (!IsBorderActivated)
            {
                if (_timer >= _mapConfig.TimeToStartBorder)
                {
                    IsBorderActivated = true;
                    _timer = 0; // Reset timer for the first shrink delay

                    // After activation, immediately prepare for the VERY FIRST shrink
                    if (CanShrinkFurther())
                    {
                        PrepareNextShrink();
                    }
                }
                return; // Don't do anything else until activated
            }

            // If we are currently shrinking, we only care about when it finishes.
            if (IsShrinking)
            {
                if (_timer >= _mapConfig.ShrinkDuration)
                {
                    // Shrink cycle has just finished
                    IsShrinking = false;
                    _timer = 0; // Reset timer for the next shrink delay

                    // IMPORTANT: Update the current width AFTER the shrink is complete
                    _currentWidth -= _mapConfig.ShrinkSize;
                    _shrinkCount++;

                    // After finishing a shrink, immediately prepare for the NEXT one
                    if (CanShrinkFurther())
                    {
                        PrepareNextShrink();
                    }
                    else
                    {
                        // No more shrinking possible, hide the indicator for good
                        if (_nextBorderIndicator != null)
                        {
                            _nextBorderIndicator.gameObject.SetActive(false);
                        }
                    }
                }
            }
            // If we are NOT shrinking, we are in a delay/rest period.
            else
            {
                // Check if the rest period is over AND if we are able to shrink further
                if (CanShrinkFurther() && _timer >= _mapConfig.ShrinkDelay)
                {
                    Shrink(); // Start the shrink animation
                }
            }
        }

        /// <summary>
        /// A new helper function to set up the indicator for the upcoming shrink.
        /// This centralizes the logic for showing the next border.
        /// </summary>
        private void PrepareNextShrink()
        {
            if (_nextBorderIndicator != null)
            {
                _nextBorderIndicator.gameObject.SetActive(true);

                // The indicator's size should be the size the border WILL BE after the next shrink.
                float nextWidth = _currentWidth - _mapConfig.ShrinkSize;
                _nextBorderIndicator.DOScale(new Vector3(nextWidth, nextWidth, 0), 1f).SetEase(Ease.OutQuad);

                // This part is to maintain the line thickness of the indicator
                for (var i = 0; i < _nextBorderIndicator.childCount; i++)
                {
                    _nextBorderIndicator.GetChild(i).DOScaleX(_initialLineScale, 1f);
                }
            }
        }

        /// <summary>
        /// Executes the shrink animation.
        /// </summary>
        private void Shrink()
        {
            IsShrinking = true;
            _timer = 0; // Reset timer to track shrink duration

            // Hide the indicator while the border is actively shrinking to avoid confusion
            if (_nextBorderIndicator != null)
            {
                _nextBorderIndicator.gameObject.SetActive(false);
            }

            float shrinkAmountPerSide = _mapConfig.ShrinkSize / 2f;
            _leftBorder.DOMoveX(_leftBorder.position.x + shrinkAmountPerSide, _mapConfig.ShrinkDuration).SetEase(Ease.Linear);
            _rightBorder.DOMoveX(_rightBorder.position.x - shrinkAmountPerSide, _mapConfig.ShrinkDuration).SetEase(Ease.Linear);
            _topBorder.DOMoveY(_topBorder.position.y - shrinkAmountPerSide, _mapConfig.ShrinkDuration).SetEase(Ease.Linear);
            _bottomBorder.DOMoveY(_bottomBorder.position.y + shrinkAmountPerSide, _mapConfig.ShrinkDuration).SetEase(Ease.Linear);
        }

        /// <summary>
        /// Checks if the border can shrink further without going below the minimum size.
        /// </summary>
        public bool CanShrinkFurther()
        {
            return _shrinkCount < _mapConfig.NumberOfShrinks;
        }

        /// <summary>
        /// Updates the damage dealt by the dark area based on the remaining match time.
        /// </summary>
        private void UpdateDamage()
        {
            // Only start ramping up damage in the final 2/3 of the match
            if (_timeRemainingInMap <= _mapConfig.TotalMapTime * (2f / 3f))
            {
                _damageUpdateTimer += Time.deltaTime;
                if (_damageUpdateTimer >= _damageUpdateInterval)
                {
                    _damageUpdateTimer = 0f;
                    float T = _mapConfig.TotalMapTime;
                    // timeIntoFinalPhase is how long we've been inside the final 2/3 of the match
                    float timeIntoFinalPhase = (T * (2f / 3f)) - Mathf.Max(0, _timeRemainingInMap);
                    DarkAreaDamage = Mathf.Ceil(_baseDamage + (timeIntoFinalPhase / _damageRampDivisor));
                }
            }
        }

        public void SetGameStarted(bool active)
        {
            _gameStarted = active;
            if (active)
            {
                _timer = 0;
            }
        }

        #endregion

        #region UI & Helpers

        private void UpdateUI()
        {
            if (!IsBorderActivated || GameScreen.Instance == null) return;

            if (IsShrinking)
            {
                GameScreen.Instance.BorderTime.text = "Shrinking...";
            }
            else if (!CanShrinkFurther())
            {
                GameScreen.Instance.BorderTime.text = "Final Circle";
            }
            else
            {
                float timeToNextAction = _mapConfig.ShrinkDelay - _timer;
                GameScreen.Instance.BorderTime.text = $"Next: {Mathf.CeilToInt(timeToNextAction)}s";
            }
        }

        public List<Vector3> GetNextBorder()
        {
            var nextBorderDetail = new List<Vector3>();
            if (_nextBorderIndicator == null || !_nextBorderIndicator.gameObject.activeSelf) return nextBorderDetail;
            for (var i = 0; i < _nextBorderIndicator.childCount; i++)
            {
                nextBorderDetail.Add(_nextBorderIndicator.GetChild(i).position);
            }
            return nextBorderDetail;
        }

        #endregion
        
        public int GetCurrentWidth()
        {
            if (IsShrinking)
            {
                float startWidth = _currentWidth;
        
                float endWidth = _currentWidth - _mapConfig.ShrinkSize;
        
                float progress = _timer / _mapConfig.ShrinkDuration;

                float realTimeWidth = Mathf.Lerp(startWidth, endWidth, progress);
        
                return Mathf.FloorToInt(realTimeWidth);
            }
    
            return _currentWidth;
        }
        
        public bool IsPositionInSafeZone(Vector2 gridPosition) 
        {
            if (!IsBorderActivated)
            {
                return true;
            }

            Vector2 mapCenter = _mapOrigin + new Vector2(_mapWorldWidth / 2f, _mapWorldHeight / 2f);

            float halfSafeWidth = _currentWidth / 2f;
            float leftEdge = mapCenter.x - halfSafeWidth;
            float rightEdge = mapCenter.x + halfSafeWidth;
            float bottomEdge = mapCenter.y - halfSafeWidth; 
            float topEdge = mapCenter.y + halfSafeWidth;

            Vector2 worldPosition = gridPosition + new Vector2(0.5f, 0.5f);

            return worldPosition.x > leftEdge &&
                   worldPosition.x < rightEdge &&
                   worldPosition.y > bottomEdge &&
                   worldPosition.y < topEdge;
        }

        public float GetShrinkSize()
        {
            return _mapConfig != null ? _mapConfig.ShrinkSize : 0f;
        }

        public Vector2 GetMapCenter()
        {
            return _mapOrigin + new Vector2(_mapWorldWidth / 2f, _mapWorldHeight / 2f);
        }

    }
}