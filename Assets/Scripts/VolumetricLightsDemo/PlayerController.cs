using UnityEngine;

namespace VolumetricLightsDemo
{
	public class PlayerController : MonoBehaviour
	{
		private CharacterController thisCharacterController;

		private Transform cameraTransform;

		private float InpVer;

		private float InpHor;

		private float jumpTimer;

		private float yRotate;

		private Vector3 direction;

		private float sprint = 1f;

		[SerializeField]
		private float speed = 10f;

		[SerializeField]
		private float sprintMax = 2f;

		[SerializeField]
		private float jumpTime = 0.25f;

		[SerializeField]
		private float jumpSpeed = 8f;

		[SerializeField]
		private float gravity = 6f;

		[SerializeField]
		private float mouseSpeed = 6f;

		private void Start()
		{
			thisCharacterController = base.gameObject.AddComponent<CharacterController>();
			thisCharacterController.height = 2f;
			thisCharacterController.center = Vector3.up;
			thisCharacterController.stepOffset = 0.8f;
			cameraTransform = Camera.main.transform;
			cameraTransform.position = base.transform.position + Vector3.up * 2f;
			cameraTransform.transform.parent = base.transform;
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		private void Update()
		{
			InpHor = Input.GetAxis("Horizontal");
			InpVer = Input.GetAxis("Vertical");
			if (Input.GetKey(KeyCode.LeftShift))
			{
				if (sprint < sprintMax)
				{
					sprint += Time.deltaTime * 5f;
				}
			}
			else if (sprint > 1f)
			{
				sprint -= Time.deltaTime * 5f;
			}
			if (Input.GetButtonDown("Jump") && thisCharacterController.isGrounded)
			{
				jumpTimer = jumpTime;
			}
			base.transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSpeed, 0f);
			direction = base.transform.forward * InpVer + base.transform.right * InpHor;
			direction *= speed * sprint;
			if (jumpTimer > 0f)
			{
				jumpTimer -= Time.deltaTime;
				direction.y += jumpSpeed * jumpTimer;
			}
			else
			{
				direction.y -= gravity;
			}
			thisCharacterController.Move(direction * Time.deltaTime);
			yRotate += (0f - Input.GetAxis("Mouse Y")) * mouseSpeed;
			yRotate = Mathf.Clamp(yRotate, -85f, 89f);
			cameraTransform.localEulerAngles = new Vector3(yRotate, 0f, 0f);
		}
	}
}
