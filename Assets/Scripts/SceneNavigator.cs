using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    [SerializeField] private string nomeDaCenaMenu = "Menu"; 

    private void Awake()
    {
        // Garante que esse objeto não seja destruído ao trocar de cena
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            VoltarAoMenu();
        }
    }

    public void VoltarAoMenu()
    {
        SceneManager.LoadScene(nomeDaCenaMenu);
        Debug.Log("Voltando ao menu...");
    }
}