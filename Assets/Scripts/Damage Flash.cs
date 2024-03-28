using System.Collections;
using UnityEngine;

public class DamageFlash : MonoBehaviour
{
    [ColorUsage(true, true)]
    [SerializeField] private Color _FlashColor = Color.white;
    [SerializeField] private float _FlashTime = 0.25f;
    [SerializeField] private AnimationCurve _FlashSpeedCurve;


    private SpriteRenderer[] _spriteRenderers;
    private Material[] _materials;
    private Coroutine _DamageFlashCoroutine;

    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        InitMaterials();
    }

    private void InitMaterials()
    {
        _materials = new Material[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _materials[i] = _spriteRenderers[i].material;
        }
    }

    public IEnumerator DamageFlasher()
    {
        SetFlashColor();

        float currentFlashAmount = 0;
        float elapsedTime = 0f;
        while (elapsedTime < _FlashTime)
        {
            elapsedTime += Time.deltaTime;
            currentFlashAmount = Mathf.Lerp(1f, _FlashSpeedCurve.Evaluate(elapsedTime), elapsedTime / _FlashTime);

            SetFlashAmount(currentFlashAmount);
            yield return null;
        }
        SetFlashAmount(0f);
    }

    private void SetFlashColor()
    {
        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i].SetColor("_FlashColor", _FlashColor);
        }
    }

    private void SetFlashAmount(float amount)
    {
        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i].SetFloat("_FlashAmount", amount);
        }
    }

    public void FlashDamage()
    {
        _DamageFlashCoroutine = StartCoroutine(DamageFlasher());
    }
}
