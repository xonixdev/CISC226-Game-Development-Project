using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EnemyStats : MonoBehaviour
{
    public float maxHealth;
    public float health;

    private RangedEnemy _RangedEnemy;
    private FlyingEnemy _FlyingEnemy;
    private bool isDead = false;

    public float coinDrop;
    public GameObject enemy;
    // Start is called before the first frame update

    private void Awake()
    {
        _RangedEnemy = GetComponent<RangedEnemy>();
        _FlyingEnemy = GetComponent<FlyingEnemy>();

    }
    void Start()
    {
        health = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        if(health == 0)
        {
            die();
        }
    }

    void die()
    {
        if (isDead) return;
        isDead = true;

        if (_RangedEnemy != null)
        {
            _RangedEnemy.DeathSequence();
        }
        else if (_FlyingEnemy != null)
        {
            _FlyingEnemy.DeathSequence();
        }
        else
        {
            Debug.LogError("No RangedEnemy or FlyingEnemy component is attached to the object");
        };
        //Drop coins
        PlayerStats playerStats = GameObject.Find("Player").GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.coins += coinDrop;
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found on Player object");
        }
    }
}
