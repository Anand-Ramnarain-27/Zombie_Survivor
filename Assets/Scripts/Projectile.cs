using UnityEngine;

public class Projectile : MonoBehaviour
{
	[SerializeField]
	private float speed = 10f;

	[SerializeField]
	private float projectileLifeTime = 2f;

	[SerializeField]
	private float damage;

	[SerializeField]
	private float critChance;

	[SerializeField]
	private float critAmount = 1f;

	public bool targetEnemy;

	public int projectilePiercing;

	public string tagToHit;

	public void SetStats(float newSpeed, float _damage, int _projectilePiercing, float _critChance, float _critAmount)
	{
		speed = newSpeed;
		damage = _damage;
		projectilePiercing = _projectilePiercing;
		critChance = _critChance;
		critAmount = _critAmount;
	}

	private void Update()
	{
		float num = (Sandevistan.instance.sandevistanActive ? (speed / 20f) : speed);
		base.transform.Translate(Vector3.forward * Time.deltaTime * num);
		projectileLifeTime -= (Sandevistan.instance.sandevistanActive ? (Time.deltaTime / 20f) : Time.deltaTime);
		if (projectileLifeTime <= 0f)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.gameObject.tag == tagToHit)
		{
			IDamageable component = other.gameObject.GetComponent<IDamageable>();
			float num = damage;
			if (critChance > (float)Random.Range(0, 100))
			{
				num *= critAmount;
			}
			component.TakeDamage(num);
			projectilePiercing--;
			if (projectilePiercing < 0)
			{
				Object.Destroy(base.gameObject);
			}
		}
		else if (other.gameObject.tag == "wall")
		{
			Object.Destroy(base.gameObject);
		}
	}
}
