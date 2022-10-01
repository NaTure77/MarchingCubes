using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionUI : MonoBehaviour
{
    public GameObject optionPanel;
    // Start is called before the first frame update

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            optionPanel.SetActive(!optionPanel.activeSelf);
            Cursor.visible = optionPanel.activeSelf;
            Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
            //Time.timeScale = optionPanel.activeSelf ? 0 : 1;
        }
    }

    public void ExitApplication()
    {
        Application.Quit();
    }

    public void SetWindowModeScreen()
    {
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }

    public void SetFullModeScreen()
    {
        Screen.SetResolution(Screen.currentResolution.width,Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
    }
}
