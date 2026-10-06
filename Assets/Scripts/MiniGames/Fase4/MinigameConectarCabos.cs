using UnityEngine;
using System.Collections.Generic;

public class MinigameConectarCabos : MinigameBase
{
    [SerializeField] private List<SlotDestino> slots = new List<SlotDestino>();

    public void VerificarVitoria()
    {
        foreach (var slot in slots)
            if (!slot.EstaConectado()) return;

        Vencer();
    }
}