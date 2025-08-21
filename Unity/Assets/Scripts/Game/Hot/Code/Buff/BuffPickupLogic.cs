using UnityGameFramework.Runtime;
using UnityEngine;

namespace Game.Hot
{
    public class BuffPickupLogic : EntityLogic
    {
        private void OnTriggerEnter(Collider other)
        {
            MyAircraft playerAircraft = other.GetComponent<MyAircraft>();
            if (playerAircraft == null)
            {
                return;
            }

            BuffComponent buffComponent = playerAircraft.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                // This should not happen as we are adding it in MyAircraft.OnShow
                return;
            }

            buffComponent.AddBuff(new SeekingBulletBuff(), playerAircraft, 10f);
            
            GameEntry.Entity.HideEntity(this.Entity);
        }
    }
}
