using UnityEngine;
using UnityEngine.UI;

public class ItemHudPresenter
{
    private readonly Animator rouletteAnimator;
    private readonly Animator scrollAnimator;
    private readonly Image itemImage;
    private readonly AudioSource rouletteAudio;
    private readonly AudioSource lockedAudio;

    public ItemHudPresenter(GameObject itemUI, Image itemIcon, AudioSource rouletteSound, AudioSource lockedSound)
    {
        itemImage = itemIcon;
        rouletteAudio = rouletteSound;
        lockedAudio = lockedSound;

        if (itemUI != null)
        {
            rouletteAnimator = itemUI.GetComponent<Animator>();
            if (itemUI.transform.childCount > 0 &&
                itemUI.transform.GetChild(0).childCount > 0)
            {
                scrollAnimator = itemUI.transform.GetChild(0).GetChild(0).GetComponent<Animator>();
            }
        }
    }

    public void StartRoulette()
    {
        rouletteAnimator?.SetBool("StartSelecting", true);
        scrollAnimator?.SetBool("Scroll", true);
        rouletteAudio?.Play();
    }

    public void StopRoulette()
    {
        rouletteAnimator?.SetBool("StartSelecting", false);
        scrollAnimator?.SetBool("Scroll", false);
    }

    public void SetItemSprite(Sprite sprite)
    {
        if (itemImage != null)
        {
            itemImage.sprite = sprite;
            itemImage.color = sprite != null ? Color.white : Color.clear;
        }
    }

    public void PlayLocked()
    {
        if (rouletteAudio != null)
        {
            rouletteAudio.Stop();
        }
        lockedAudio?.Play();
    }
}

