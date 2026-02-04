using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CrescentWreath.Data;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CrescentWreath.View
{
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public class UICardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 组件引用")]
        public RawImage artImage;

        [Header("交互参数")]
        public float hoverScale = 1.5f;      // 放大倍数
        public float hoverYOffset = 80f;     // 向上浮动的距离
        public float animationSpeed = 20f;   // 动画响应速度 (值越大越快)

        // 运行时数据
        public BaseCardSO _cardData;
        private AsyncOperationHandle<Texture2D> _textureHandle;
        
        // 布局与渲染控制
        private RectTransform _rectTransform;
        private Canvas _canvas; // 用于控制渲染层级
        private bool _isHovering = false;
        
        // 外部（LayoutManager）计算出的目标状态
        public Vector2 LayoutPosition { get; set; }
        public Quaternion LayoutRotation { get; set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponent<Canvas>();
            
            // 确保初始状态不覆盖层级，完全听从父Canvas
            if (_canvas != null)
            {
                _canvas.overrideSorting = false; 
            }
        }

        public void Setup(BaseCardSO data)
        {
            _cardData = data;
            LoadCardImage();
        }

        private void LoadCardImage()
        {
            if (_textureHandle.IsValid()) Addressables.Release(_textureHandle);
            if (_cardData == null) return;

            string key = _cardData.cardId.ToString();
            _textureHandle = Addressables.LoadAssetAsync<Texture2D>(key);
            _textureHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (artImage != null)
                    {
                        artImage.texture = handle.Result;
                        artImage.color = Color.white;
                    }
                }
            };
        }

        private void OnDisable()
        {
            if (_textureHandle.IsValid()) Addressables.Release(_textureHandle);
        }

        private void Update()
        {
            // --- 核心动画状态机 ---
            
            Vector2 finalPos;
            Quaternion finalRot;
            Vector3 finalScale;

            if (_isHovering)
            {
                // 状态：悬停
                // 位置：基于布局位置向上浮动
                // 旋转：归零 (变正)
                // 缩放：放大
                finalPos = LayoutPosition + new Vector2(0, hoverYOffset);
                finalRot = Quaternion.identity; 
                finalScale = Vector3.one * hoverScale;
            }
            else
            {
                // 状态：正常
                // 完全听从 HandLayoutManager 的指挥
                finalPos = LayoutPosition;
                finalRot = LayoutRotation;
                finalScale = Vector3.one;
            }

            // 平滑插值 (Lerp) 让动作丝滑
            float dt = Time.deltaTime * animationSpeed;
            _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, finalPos, dt);
            _rectTransform.localRotation = Quaternion.Lerp(_rectTransform.localRotation, finalRot, dt);
            _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, finalScale, dt);
        }

        // === 鼠标交互 ===

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;

            // 【关键】开启 Canvas 重写，强行把 SortingOrder 提至最高
            // 这会让它在视觉上覆盖所有其他 UI，但不会改变 Hierarchy 顺序
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 100; 
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;

            // 恢复原状，把渲染权交还给父 Canvas
            if (_canvas != null)
            {
                _canvas.overrideSorting = false;
                _canvas.sortingOrder = 0;
            }
        }
    }
}