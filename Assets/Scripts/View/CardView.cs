using UnityEngine;
using System.Collections;
using CrescentWreath.Core;
using CrescentWreath.Data;
// [修正] 引入 Addressables 命名空间
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CrescentWreath.View
{
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        [Header("视觉组件")]
        public MeshRenderer frontRenderer;

        [Header("运行时数据")]
        public BaseCardSO cardData;

        // [修正] 持有句柄以便释放内存
        private AsyncOperationHandle<Texture2D> _textureHandle;
        private static MaterialPropertyBlock _propBlock;
        private bool _isHovering = false;

        [Header("状态追踪")]
        public int currentOwnerId = -1;
        public ZoneType currentZone = ZoneType.Unknown;

        /*private void Start()
        {
            // 如果你在编辑器里手动挂了 data，这行代码会让它在运行时点火
            if (cardData != null)
            {
                Setup(cardData);
            }
        }*/
        public void Setup(BaseCardSO data)
        {
            this.cardData = data;

            // 1. 释放旧的图片内存 (如果是对象池复用的情况)
            if (_textureHandle.IsValid())
            {
                Addressables.Release(_textureHandle);
            }

            // 2. 构造 Addressable Key
            // 请确保你在 Inspector 勾选 Addressable 时，Address 填的是 "23024" 这种格式
            // 或者你可以用 label，这里我们假设 Address == 文件名
            string key = $"{data.cardId}";
            Debug.Log($"[CardView] 加载卡牌图片，Addressable Key: {key}");

            // 3. 异步加载图片
            _textureHandle = Addressables.LoadAssetAsync<Texture2D>(key);
            _textureHandle.Completed += OnTextureLoaded;
        }
        // 【新增】用于更新卡牌的归属状态
        public void UpdateState(int ownerId, ZoneType zone)
        {
            this.currentOwnerId = ownerId;
            this.currentZone = zone;
        }
        private void OnTextureLoaded(AsyncOperationHandle<Texture2D> handle)
        {
            Debug.Log($"[CardView] 加载状态: {handle.Status} | Key: {cardData.cardId}");
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[CardView] 成功拿到贴图: {handle.Result.name}");
                ApplyTexture(handle.Result);
            }
            else
            {
                // 这里的报错会告诉你究竟是找不到资源，还是其他原因
                Debug.LogError($"[CardView] 加载失败异常: {handle.OperationException}");
            }
        }

        private void ApplyTexture(Texture2D texture)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            if (frontRenderer != null)
            {
                frontRenderer.GetPropertyBlock(_propBlock);

                // 1. 自动匹配 Shader 属性名 (URP通常是 _BaseMap)
                string propertyName = "_MainTex";
                if (frontRenderer.sharedMaterial.HasProperty("_BaseMap"))
                    propertyName = "_BaseMap";

                // 2. 设置贴图
                _propBlock.SetTexture(propertyName, texture);

                // =========================================================
                // 【修复】 垂直翻转 UV，矫正倒置的图片
                // Vector4 参数含义: (Tiling X, Tiling Y, Offset X, Offset Y)
                // (1, -1, 0, 1) 的意思是：横向不变，纵向倒过来，并把位置拉回正轨
                // =========================================================

                _propBlock.SetVector(propertyName + "_ST", new Vector4(-1, -1, 1, 1));

                frontRenderer.SetPropertyBlock(_propBlock);

                Debug.Log($"<color=yellow>[CardView]</color> 已应用贴图并修正UV翻转: {propertyName}");
            }
        }

        private void OnDisable()
        {
            // [修正] 极其重要：物体被隐藏或销毁时，释放显存！
            if (_textureHandle.IsValid())
            {
                Addressables.Release(_textureHandle);
            }

            _isHovering = false;
        }

        // ========================================================================
        // 交互逻辑 (保持 TTS 风格不变)
        // ========================================================================

        private void OnMouseEnter() { _isHovering = true; }

        private void OnMouseExit()
        {
            _isHovering = false;
            GameEvent.Request_HideCardTooltip?.Invoke();
            GameEvent.Request_HideZoom?.Invoke();
        }

        private void OnMouseOver()
        {
            if (cardData == null) return;

            if (Input.GetKey(KeyCode.LeftControl))
            {
                GameEvent.Request_HideCardTooltip?.Invoke();
                GameEvent.Request_ZoomCard?.Invoke(cardData);
            }
            else
            {
                GameEvent.Request_HideZoom?.Invoke();
                GameEvent.Request_ShowCardTooltip?.Invoke(cardData, Input.mousePosition);
            }
        }

        // ========================================================================
        // 运动逻辑 (保持不变)
        // ========================================================================
        public void MoveTo(Vector3 position, Quaternion rotation, float duration = 0.4f)
        {
            StopAllCoroutines();
            StartCoroutine(MoveCoroutine(position, rotation, duration));
        }

        private IEnumerator MoveCoroutine(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                float easeT = 1f - Mathf.Pow(1f - t, 3);

                transform.position = Vector3.Lerp(startPos, targetPos, easeT);
                transform.rotation = Quaternion.Lerp(startRot, targetRot, easeT);
                yield return null;
            }
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }
}