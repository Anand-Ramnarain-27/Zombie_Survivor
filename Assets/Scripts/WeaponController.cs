using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
	public Transform weaponHolder;

	public List<Weapon> possibleWeapons;

	[SerializeField]
	private List<Weapon> equippedWeapons = new List<Weapon>();

	private PlayerManager playerManager;

	public Weapon selectedWeapon => equippedWeapons[0];

	public int WeaponCount => equippedWeapons.Count;

	private void Awake()
	{
		playerManager = GetComponent<PlayerManager>();
	}

	public void LoadWeapon(WeaponType weaponType)
	{
		for (int i = 0; i < possibleWeapons.Count; i++)
		{
			if (!(possibleWeapons[i].weaponType == weaponType))
			{
				continue;
			}
			if (!equippedWeapons.Contains(possibleWeapons[i]))
			{
				EquipWeapon(possibleWeapons[i]);
				break;
			}
			for (int j = 0; j < equippedWeapons.Count; j++)
			{
				if (equippedWeapons[j].weaponType == weaponType)
				{
					equippedWeapons[j].LevelUp();
					return;
				}
			}
		}
	}

	public void EquipWeapon(Weapon weaponToEquip)
	{
		Debug.Log("Equipping weapon: " + weaponToEquip.name);
		equippedWeapons.Add(Object.Instantiate(weaponToEquip, weaponHolder));
	}


	public void Shoot()
	{
		if (playerManager.invulnerable || playerManager.isReloading || equippedWeapons.Count == 0 || OutOfAmmo())
		{
			return;
		}

		foreach (Weapon equippedWeapon in equippedWeapons)
		{
			equippedWeapon.Shoot();
		}
	}


	public bool CanReload()
	{
		if (equippedWeapons[0].AmmoCount < equippedWeapons[0].MaxAmmoCount)
		{
			return true;
		}
		return false;
	}

	public bool OutOfAmmo()
	{
		if (equippedWeapons[0].AmmoCount <= 0)
		{
			StartCoroutine(PlayerManager.instance.Reload());
			return true;
		}
		return false;
	}

	public float GetReloadSpeed()
	{
		return equippedWeapons[0].ReloadSpeed;
	}

	public void SelectWeapon(int slotNumber)
	{
	}

	public void ToggleFireMode()
	{
		selectedWeapon.weaponUI.ToggleFireMode();
	}

	public void OpenSwordCollider()
	{
		equippedWeapons[0].swordCollider.collider.enabled = true;
		equippedWeapons[0].swordtrail.Play();
	}

	public void CloseSwordCollider()
	{
		equippedWeapons[0].swordCollider.collider.enabled = false;
	}
}
