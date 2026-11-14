using UnityEngine;

/// <summary>
/// Coloque um em cada cena para definir o clima da música ao entrar
/// (MainMenu = Menu, cena do jogo = Gameplay, tela de derrota = GameOver...).
/// </summary>
public class SceneMusicState : MonoBehaviour
{
    public AudioManager.MusicState state = AudioManager.MusicState.Menu;
    public float fade = 1f;
    [Tooltip("1 = velocidade normal. Na partida, use GameManager para subir com a dificuldade.")]
    public float musicSpeed = 1f;

    void Start()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        am.SetState(state, fade);
        am.SetMusicSpeed(musicSpeed, fade);
    }
}
