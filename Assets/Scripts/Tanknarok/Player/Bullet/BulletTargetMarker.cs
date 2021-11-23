using UnityEngine;

namespace FusionExamples.Tanknarok
{
	public class BulletTargetMarker : MonoBehaviour, Bullet.ITargetVisuals
	{
		[SerializeField] private ParticleSystem _targetMarker;

		private Vector3 _position;

		public void Destroy()
		{
			if(_targetMarker && _targetMarker.gameObject)
				Destroy(_targetMarker.gameObject);
		}

		public void LateUpdate()
		{
			UpdateTargetMarkerPosition();
		}

		private void UpdateTargetMarkerPosition()
		{
			if (_targetMarker)
			    _targetMarker.transform.position = _position;
		}

		public void InitializeTargetMarker(Vector3 bulletVelocity, Bullet.BulletSettings bulletSettings)
		{
			_targetMarker.transform.SetParent(null);
			_targetMarker.gameObject.SetActive(true);
			_position = CalculateImpactPoint(bulletVelocity, bulletSettings);
			UpdateTargetMarkerPosition();
			_targetMarker.Play();
		}

		Vector3 CalculateImpactPoint(Vector3 initialVelocity, Bullet.BulletSettings bulletSettings)
		{
			Vector3 originPosition = transform.position;
			Vector3 pos = originPosition;
			Vector3 prevPos = originPosition;
			Vector3 velocity = initialVelocity;
			float t = 0;

			for (float i = 0; i < bulletSettings.timeToLive; i += Time.deltaTime)
			{
				t = bulletSettings.timeToLive * i;
				pos = (originPosition + velocity * t) + ((Vector3.up * bulletSettings.gravity) * Mathf.Pow(t, 2) * 0.5f);

				if (RaycastTargetMarker(prevPos, pos - prevPos, bulletSettings.hitMask, out pos))
				{
					break;
				}

				prevPos = pos;
			}

			return pos + Vector3.up * 0.05f; //Return the position with a slight y offset to avoid spawning the marker inside whatever it hits
		}

		bool RaycastTargetMarker(Vector3 position, Vector3 direction, LayerMask collisionMask, out Vector3 hitPosition)
		{
			Ray ray = new Ray(position, direction);
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit, direction.magnitude, collisionMask))
			{
				hitPosition = hit.point;
				return true;
			}
			else
			{
				hitPosition = position + direction;
				return false;
			}
		}
	}
}