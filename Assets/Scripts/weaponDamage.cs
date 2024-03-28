using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weaponDamage : MonoBehaviour
{
    public float damage = 1;

    public float knockbackForce;
    public GameObject player;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // OnTriggerEnter is called when the Collider other enters the trigger
    void OnTriggerEnter2D(Collider2D other)
    {
        
        // Check if the trigger collision involves the enemy
        if (other.gameObject.CompareTag("Enemy"))
        {

            EnemyStats enemyStats = other.GetComponent<EnemyStats>();

            enemyStats.health = enemyStats.health - damage;

            //Enemy Damage Flash
            other.gameObject.GetComponent<DamageFlash>()?.FlashDamage();

            //FIX
            Vector2 knockback = other.transform.position - player.transform.position;
            other.GetComponent<Rigidbody2D>().AddForce(knockback * knockbackForce, ForceMode2D.Impulse);
        }
    }
}
