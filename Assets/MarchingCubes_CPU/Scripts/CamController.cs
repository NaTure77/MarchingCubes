using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCube_CPU
{
    public class CamController : MonoBehaviour
    {
        // Start is called before the first frame update

        // Update is called once per frame

        float rot_x;
        float rot_y;

        float sensitivity = 2;

        public float moveSpeed = 3;

        Camera cam;
        void Awake()
        {
            cam = Camera.main;
        }
        void Update()
        {
            rot_x += Input.GetAxis("Mouse X") * sensitivity;
            rot_y -= Input.GetAxis("Mouse Y") * sensitivity;

            rot_y = Mathf.Clamp(rot_y, -90, 90);
            transform.eulerAngles = new Vector3(rot_y, rot_x, 0);
            Vector3 direction = new Vector3();
            if (Input.GetKey(KeyCode.W))
            {
                direction += Vector3.forward;
            }
            if (Input.GetKey(KeyCode.A))
            {
                direction += Vector3.left;
            }
            if (Input.GetKey(KeyCode.S))
            {
                direction += Vector3.back;
            }
            if (Input.GetKey(KeyCode.D))
            {
                direction += Vector3.right;
            }
            if (Input.GetKey(KeyCode.V))
            {
                direction += Vector3.up;
            }
            if (Input.GetKey(KeyCode.C))
            {
                direction += Vector3.down;
            }

            Vector3 forward = Quaternion.Euler(0, rot_x, 0) * Vector3.forward * direction.z;
            transform.position += (forward + transform.right * direction.x + Vector3.up * direction.y).normalized * moveSpeed * Time.deltaTime;



            if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(UseBrush(KeyCode.Space, false));
            if (Input.GetKeyDown(KeyCode.X)) StartCoroutine(UseBrush(KeyCode.X, true));
        }

        float maxDistance = 1000;
        IEnumerator UseBrush(KeyCode inputKey, bool eraseMode)
        {
            Vector2 input = new Vector2(Screen.width / 2.0f, Screen.height / 2.0f);
            RaycastHit hit;
            while (Input.GetKey(inputKey))
            {
                Ray ray = cam.ScreenPointToRay(input);

                if (Physics.Raycast(ray, out hit, maxDistance))// && !hit.collider.tag.Equals("Untagged"))
                {
                    MapGenerator.instance.UseBrush(hit.point, eraseMode);
                }
                yield return null;
            }
        }
    }
}