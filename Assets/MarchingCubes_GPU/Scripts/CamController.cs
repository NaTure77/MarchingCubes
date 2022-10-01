using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCube_GPU
{
    public class CamController : MonoBehaviour
    {
        // Start is called before the first frame update

        // Update is called once per frame

        //public GameObject body;
        float rot_x;
        float rot_y;

        public float rotSpeed = 2;

        public float moveSpeed = 3;

        Camera cam;


        void Awake()
        {
            cam = Camera.main;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Application.targetFrameRate = 60;
        }
        void Update()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
			rot_x += Input.GetAxis("Mouse X") * rotSpeed;
            rot_y -= Input.GetAxis("Mouse Y") * rotSpeed;
#elif UNITY_ANDROID
            if (Input.touchCount > 0)
            {
                Vector2 i= Input.GetTouch(0).deltaPosition;
                rot_x += i.x * sensitivity * 0.5f;
                rot_y -= i.y * sensitivity * 0.5f;
            }
			
#endif

			
            //rot_x += Input.GetAxis("Mouse X") * sensitivity;
            //rot_y -= Input.GetAxis("Mouse Y") * sensitivity;

            rot_y = Mathf.Clamp(rot_y, -90, 90);

            Quaternion rot = Quaternion.Euler(new Vector3(rot_y, rot_x, 0));
            transform.rotation = Quaternion.Lerp(transform.rotation, rot, 0.3f);

            //transform.eulerAngles = new Vector3(rot_y, rot_x % 360, 0);
            Vector3 direction = new Vector3();
            if (Input.GetKey(KeyCode.Space))
            {
                direction += Vector3.up;
            }
            if (Input.GetKey(KeyCode.LeftShift))
            {
                direction += Vector3.down;
            }

            direction.x = Input.GetAxis("Horizontal");
            direction.z = Input.GetAxis("Vertical");

            Vector3 forward = Quaternion.Euler(0, rot_x, 0) * Vector3.forward * direction.z;

            Vector3 nextPosition = transform.position + (forward + transform.right * direction.x + Vector3.up * direction.y) * moveSpeed * Time.deltaTime;
            transform.position = nextPosition;//Vector3.Lerp(transform.position,nextPosition, 0.3f);



            if (Input.GetKeyDown(KeyCode.Mouse0)) StartCoroutine(UseBrush(KeyCode.Mouse0, false));
            if (Input.GetKeyDown(KeyCode.Mouse1)) StartCoroutine(UseBrush(KeyCode.Mouse1, true));
            if (Input.GetKey(KeyCode.E)) MakeSphere();
        }
        void MakeSphere()
        {
            MapGenerator.instance.UseBrush(transform.position, false);
        }
        IEnumerator UseBrush(KeyCode inputKey, bool eraseMode)
        {
            Vector2 input = new Vector2(Screen.width / 2.0f, Screen.height / 2.0f);
            RaycastHit hit;
            while (Input.GetKey(inputKey))
            {
                Ray ray = cam.ScreenPointToRay(input);

                if (Physics.Raycast(ray, out hit, 300))// && !hit.collider.tag.Equals("Untagged"))
                {
                    //PointIndicator.position = hit.point;
                   MapGenerator.instance.UseBrush(hit.point, eraseMode);
                }
                yield return null;
            }
        }
    }
}