using System.Collections;
using TJAudio;
using UnityEngine;

public class Weapon : MonoBehaviour
{
	public enum WeaponClass
	{
		pistol = 0,
		rifle = 1,
		melee = 2
	}

	public WeaponType weaponType;

	public Sprite icon;

	public Projectile projectile;

	private float gunRateOfFire;

	private float muzzleVelocity;

	private float reloadSpeed;

	private float projectileVariance;

	private float damage;

	private int projectilePiercing;

	private int ammoCount;

	private int maxAmmoCount;

	private float nextShotTime;

	private int level;

	private Coroutine flashOnHit;

	private WaitForSeconds flashWait;

	[HideInInspector]
	public WeaponUI weaponUI;

	public WeaponClass weaponClass;

	[Header("Sword")]
	public SwordCollider swordCollider;

	public ParticleSystem swordtrail;

	private int swingIndex;

	private string[] anims = new string[2] { "SwingSword", "SwingSword2" };

	[SerializeField]
	private Light muzzzleFlash;

	private float flashDuration = 0.02f;

	private bool loaded;

	public int AmmoCount => ammoCount;

	public int MaxAmmoCount => maxAmmoCount;

	public float ReloadSpeed => reloadSpeed;

	private void Start()
	{
		LoadBaseStats();
		WeaponManager.instance.LoadWeaponUI(this);
		flashWait = new WaitForSeconds(flashDuration);
		if (weaponClass == WeaponClass.pistol)
		{
			PlayerManager.instance.playerMovement.PlayTargetAnimation("PistolIdle", isInteracting: false);
		}
		else if (weaponClass == WeaponClass.pistol)
		{
			PlayerManager.instance.playerMovement.PlayTargetAnimation("RifleIdle", isInteracting: false);
		}
		else if (weaponClass == WeaponClass.melee)
		{
			PlayerManager.instance.playerMovement.PlayTargetAnimation("SwordIdle", isInteracting: false);
		}
		loaded = true;
	}

	private void LoadBaseStats()
	{
		if (WeaponManager.instance.weaponDictionary.TryGetValue(weaponType, out var value))
		{
			projectilePiercing = value[level].projectilePiercing;
			gunRateOfFire = value[level].gunRateOfFire;
			damage = value[level].damage;
			muzzleVelocity = value[level].muzzleVelocity;
			projectileVariance = value[level].projectileVariance;
			reloadSpeed = value[level].reloadSpeed;
			maxAmmoCount = (int)value[level].maxAmmoCount;
			ammoCount = maxAmmoCount;
		}
		else
		{
			Debug.Log($"Weapon type {weaponType} not found in dictionary");
		}
	}

	public void Shoot()
	{
		if (!loaded || !(Time.time > nextShotTime))
		{
			return;
		}
		nextShotTime = Time.time + gunRateOfFire / PlayerManager.instance.playerStats.RateOfFire;
		if (weaponClass == WeaponClass.pistol || weaponClass == WeaponClass.rifle)
		{
			MuzzleFlash();
			if (weaponClass == WeaponClass.pistol)
			{
				//IAudioRequester.instance.PlaySFX("firePistol");
			}
			else if (weaponClass == WeaponClass.rifle)
			{
				//IAudioRequester.instance.PlaySFX("fireRifle");
			}
			float y = Random.Range(0f - projectileVariance, projectileVariance);
			Quaternion rotation = PlayerManager.instance.transform.rotation;
			rotation *= Quaternion.Euler(0f, y, 0f);
			Object.Instantiate(projectile, PlayerManager.instance.muzzle.position, rotation).SetStats(muzzleVelocity, damage, projectilePiercing + (int)PlayerManager.instance.playerStats.ProjectilePiercing, PlayerManager.instance.playerStats.CritChance, PlayerManager.instance.playerStats.CritDamage);
			ammoCount--;
			if (weaponUI != null)
			{
				weaponUI.UpdateAmmo(ammoCount);
			}
		}
		else if (weaponClass == WeaponClass.melee)
		{
			if (swingIndex >= anims.Length)
			{
				swingIndex = 0;
			}
			PlayerManager.instance.playerMovement.PlayTargetAnimation(anims[swingIndex], isInteracting: false, canRotate: true);
			swingIndex++;
		}
	}

	public IEnumerator FlashOnHit()
	{
		muzzzleFlash.enabled = true;
		yield return flashWait;
		muzzzleFlash.enabled = false;
		flashOnHit = null;
	}

	public void MuzzleFlash()
	{
		if (flashOnHit != null)
		{
			StopCoroutine(flashOnHit);
		}
		flashOnHit = StartCoroutine(FlashOnHit());
	}

	public void LevelUp()
	{
		Debug.Log("Leveled up the weapon");
		if (WeaponManager.instance.weaponDictionary.TryGetValue(weaponType, out var value))
		{
			projectilePiercing = value[level].projectilePiercing;
			gunRateOfFire = value[level].gunRateOfFire;
			damage = value[level].damage;
			muzzleVelocity = value[level].muzzleVelocity;
			level++;
		}
		else
		{
			Debug.Log($"Weapon type {weaponType} not found in dictionary");
		}
	}

	public void ReloadWeapon()
	{
		ammoCount = maxAmmoCount;
		if (weaponUI != null)
		{
			weaponUI.UpdateAmmo(maxAmmoCount);
		}
	}
}
