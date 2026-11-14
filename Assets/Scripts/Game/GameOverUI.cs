using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Tooltip("Painel que contém MENU e TRY AGAIN")]
    public GameObject painelGameOver;

    [Tooltip("Nome exato da cena do menu")]
    public string nomeCenaMenu = "Menu";

    private void Awake()
    {
        if (painelGameOver != null)
            painelGameOver.SetActive(false);
    }

    public void MostrarGameOver()
    {
        if (painelGameOver != null)
            painelGameOver.SetActive(true);

    }

    public void TryAgain()
    {
        if (painelGameOver != null)
            painelGameOver.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ReiniciarJogo();
    }

    public void IrParaMenu()
    {
        SceneManager.LoadScene(nomeCenaMenu);
    }
}