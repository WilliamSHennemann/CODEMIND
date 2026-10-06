using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
 [SerializeField] private string nomeDaCenaParaCarregar = "Tutorial";
    
    [SerializeField] private bool isExitButton = false;

    private void OnMouseDown()
    {
        if (isExitButton)
        {
            Sair();
        }
        else
        {
            Jogar();
        }
    }

    public void Jogar()
    {
        SceneManager.LoadScene(nomeDaCenaParaCarregar);
    }

    public void Sair()
    {
        Debug.Log("Jogador clicou em Sair!");
        Application.Quit();
    }
}
