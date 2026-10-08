using UnityEngine;
using MoreMountains.Tools;
using System.Collections;

namespace Enzeun.Runtime
{

    public class LoadingHelper : MMAdditiveSceneLoadingManager
    {
        public void ActivateScene()
        {
            if (_loadDestinationAsyncOperation != null)
            {
                _loadDestinationAsyncOperation.allowSceneActivation = true;
            }
        }

        protected override IEnumerator DestinationSceneActivation()
        {
            yield return MMCoroutine.WaitForFrames(1);
            //_loadDestinationAsyncOperation.allowSceneActivation = true;
            while (_loadDestinationAsyncOperation.progress < 1.0f)
            {
                yield return null;
            }
            MMLoadingSceneDebug("MMLoadingSceneManagerAdditive : activating destination scene");
            MMSceneLoadingManager.LoadingSceneEvent.Trigger(_sceneToLoadName, MMSceneLoadingManager.LoadingStatus.DestinationSceneActivation);
            OnDestinationSceneActivation?.Invoke();
        }

    }

}