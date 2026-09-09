using UnityEngine;

public class SlotDestino : MonoBehaviour
{
    public string idEsperado;
    private bool conectado = false;

    public void Conectado()
    {
        conectado = true;
        GetComponentInParent<MinigameConectarCabos>()?.VerificarVitoria();
    }

    public bool EstaConectado() => conectado;
}