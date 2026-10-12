using UnityEngine;
using Photon.Pun;
using Photon.Realtime;


namespace ENZEUN.Runtime
{
    public class PhotonLinkManager : MonoBehaviour
    {

        private void Start()
        {
            PhotonNetwork.ConnectUsingSettings();

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}