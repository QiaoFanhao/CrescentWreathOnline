using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CrescentWreath.View
{
    /// <summary>
    /// Read-only UI card view used in battlefield UI panels.
    /// No "play card on click" behavior is attached here.
    /// </summary>
    public class BattlefieldUICardView : MonoBehaviour
    {
        [Header("UI References")]
        public RawImage artImage;

        [Header("Runtime Data")]
        public BaseCardSO cardData;

        private AsyncOperationHandle<Texture2D> _textureHandle;

        public void Setup(BaseCardSO data)
        {
            cardData = data;
            LoadCardImage();
        }

        private void LoadCardImage()
        {
            if (_textureHandle.IsValid())
            {
                Addressables.Release(_textureHandle);
            }

            if (cardData == null)
            {
                if (artImage != null) artImage.texture = null;
                return;
            }

            string key = cardData.cardId.ToString();
            _textureHandle = Addressables.LoadAssetAsync<Texture2D>(key);
            _textureHandle.Completed += OnTextureLoaded;
        }

        private void OnTextureLoaded(AsyncOperationHandle<Texture2D> handle)
        {
            if (artImage == null) return;
            if (handle.Status != AsyncOperationStatus.Succeeded) return;

            artImage.texture = handle.Result;
            artImage.color = Color.white;
        }

        private void OnDisable()
        {
            if (_textureHandle.IsValid())
            {
                Addressables.Release(_textureHandle);
            }
        }
    }
}
