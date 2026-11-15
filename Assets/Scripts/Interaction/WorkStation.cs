using System;
using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The player's shift task. Each E press sorts one ore chunk with a fixed two-handed animation:
    /// the player is lined up with the belt, takes the chunk, turns to the bin and tosses it in,
    /// then the next chunk rides in from the feeder.
    /// </summary>
    public class WorkStation : Interactable
    {
        public int required = 3;
        public Transform ore;          // the chunk in front of the player
        public Transform feedPoint;    // where a new chunk arrives from
        public Transform workPoint;    // where it waits to be sorted
        public Transform binPoint;     // where it lands
        public float facingYaw = 180f; // which way the player faces while working (toward the belt)

        public int Done { get; private set; }
        public bool QuotaMet => Done >= required;
        public event Action<int, int> Progress;   // done, required

        bool busy, endless;

        public override string Prompt => busy ? null : "Sort ore";
        public override bool IsAvailable => !QuotaMet || endless;
        public override Vector3 Position => workPoint.position;

        public override void Interact(PlayerInteractor player)
        {
            if (busy || !IsAvailable) return;
            StartCoroutine(SortOne(player));
        }

        /// <summary>
        /// The shift never really ends: once the mask is off the belt, ore keeps arriving at the
        /// station and can still be sorted, so the line looks alive for the rest of the round.
        /// </summary>
        public void KeepFeeding()
        {
            if (endless) return;
            endless = true;
            StartCoroutine(NextChunk());
        }

        IEnumerator NextChunk()
        {
            busy = true;
            ore.gameObject.SetActive(true);
            ore.rotation = Quaternion.Euler(0f, 73f * Done, 0f);
            yield return Move(feedPoint.position, workPoint.position, 0.8f, 0f);   // rides in from the feeder
            busy = false;
        }

        IEnumerator SortOne(PlayerInteractor player)
        {
            busy = true;
            if (player != null && player.body != null)
            {
                yield return player.body.Sort(ore, facingYaw);                       // hands carry it until they let go
                yield return Move(ore.position, binPoint.position, 0.32f, 0.12f);    // short toss into the bin
            }
            else
            {
                yield return Move(workPoint.position, binPoint.position, 0.4f, 0.5f);
            }

            Done++;
            Progress?.Invoke(Done, required);
            if (QuotaMet && !endless)
            {
                ore.gameObject.SetActive(false);       // the next thing down the belt is the mask
                busy = false;
            }
            else yield return NextChunk();
        }

        IEnumerator Move(Vector3 from, Vector3 to, float seconds, float arc)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3 - 2 * t);
                ore.position = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * arc);
                yield return null;
            }
            ore.position = to;
        }
    }
}
