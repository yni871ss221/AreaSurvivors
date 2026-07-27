using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class EndingCreditsSequence : MonoBehaviour
    {
        [Serializable]
        public sealed class LocalizedCreditLine
        {
            public Text target;
            [TextArea] public string japanese;
            [TextArea] public string english;
        }

        public GameObject root;
        public Animator animator;
        public AnimationClip creditsClip;
        public string stateName = "EndingCreditsScroll";
        public float bgmDurationSeconds = 56f;
        public LocalizedCreditLine[] localizedLines;

        Coroutine playRoutine;

        public bool IsPlaying => playRoutine != null;

        public void Play(Action onCompleted)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
                AudioManager.StopBgm();
            }
            if (root != null) root.SetActive(true);
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("Ending credits could not start because its Scene object is under an inactive hierarchy.");
                CompleteImmediately(onCompleted);
                return;
            }
            if (animator == null || creditsClip == null)
            {
                CompleteImmediately(onCompleted);
                return;
            }

            ApplyLocalizedTexts();
            animator.Rebind();
            animator.Update(0f);
            animator.Play(stateName, 0, 0f);
            AudioManager.PlayBgm(BgmTrack.EndingCredits);
            playRoutine = StartCoroutine(CompleteAfter(creditsClip.length, onCompleted));
        }

        void ApplyLocalizedTexts()
        {
            if (localizedLines == null) return;
            for (int i = 0; i < localizedLines.Length; i++)
            {
                var line = localizedLines[i];
                if (line == null || line.target == null) continue;
                line.target.text = LocalizationService.Text(line.japanese, line.english);
            }
        }

        IEnumerator CompleteAfter(float duration, Action onCompleted)
        {
            float musicDuration = Mathf.Clamp(bgmDurationSeconds, 0f, duration);
            if (musicDuration > 0f)
                yield return new WaitForSecondsRealtime(musicDuration);
            AudioManager.StopBgm();

            float silentDuration = Mathf.Max(0f, duration - musicDuration);
            if (silentDuration > 0f)
                yield return new WaitForSecondsRealtime(silentDuration);

            playRoutine = null;
            if (root != null) root.SetActive(false);
            onCompleted?.Invoke();
        }

        void CompleteImmediately(Action onCompleted)
        {
            if (root != null) root.SetActive(false);
            onCompleted?.Invoke();
        }

        void OnDisable()
        {
            if (playRoutine == null) return;
            StopCoroutine(playRoutine);
            playRoutine = null;
            AudioManager.StopBgm();
        }
    }
}
