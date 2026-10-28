#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using JungleMaze.App.Flow;
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.DI;
using JungleMaze.Infrastructure.Network;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleMaze.Composition
{
    // Opt-in Windows acceptance runner; never runs during ordinary play.
    public sealed class OfflineScenarioRunner : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        { public bool passed; public string error; public int rounds; public bool zeroSocket, hpDamage, replay, menuKeepsTicking, returnedToMenu; }
        private readonly Evidence _e = new Evidence();
        private OfflineMatchSession _solo;
        private ConnectionMenuPresenter _menu;
        private MatchMenuController _esc;
        private NetworkClient _net;
        private double _started, _menuAt;
        private float _before;
        private int _stage;
        private string _path;
        public void Construct(OfflineMatchSession solo, ConnectionMenuPresenter menu, MatchMenuController esc, NetworkClient net)
        { _solo=solo; _menu=menu; _esc=esc; _net=net; _started=Time.realtimeSinceStartupAsDouble;
          var args=Environment.GetCommandLineArgs(); var i=Array.IndexOf(args,"-offlineReport");
          _path=i>=0 && i+1<args.Length ? args[i+1] : Path.Combine(Application.persistentDataPath,"offline-test.json"); }
        private void Update()
        {
            if (_solo==null) return;
            try
            {
                if (Time.realtimeSinceStartupAsDouble-_started>90) throw new Exception("Offline acceptance timeout");
                if (_stage==0) { _menu.StartOffline(); _stage=1; return; }
                if (SceneManager.GetActiveScene().name!="Game") return;
                var hero=FindAnyObjectByType<HeroController>(); if(hero==null) return;
                _e.zeroSocket=_net.LocalPort==0 && _net.State==JungleMaze.Domain.Network.NetworkState.Disconnected;
                if(!_e.zeroSocket) throw new Exception("Offline opened network");
                if(_stage==1)
                {
                    hero.transform.position=new Vector3(1,1,0); hero.GetComponent<Rigidbody2D>().position=new Vector2(1,1);
                    _e.hpDamage |= _solo.PlayerHp<100 && _solo.PlayerHp>0;
                    if(!_solo.HasResult) return;
                    _e.rounds++; var old=_solo.MatchId; _solo.Replay();
                    _e.replay=old!=_solo.MatchId && _solo.PlayerHp==100 && !_solo.HasResult;
                    if(_e.rounds==1) { _esc.PressEscape(); _before=_solo.ElapsedSeconds; _menuAt=Time.realtimeSinceStartupAsDouble; _stage=2; }
                    else { _menu.Disconnect(); _e.returnedToMenu=!_solo.IsActive && _menu.Screen==AppScreen.MainMenu; Finish(null); }
                }
                else if(_stage==2 && Time.realtimeSinceStartupAsDouble-_menuAt>2)
                {
                    _e.menuKeepsTicking=_esc.IsOpen && Time.timeScale==1 && _solo.ElapsedSeconds>_before+1 && _solo.PlayerHp==100;
                    _esc.PressEscape(); _stage=1;
                }
            }
            catch(Exception ex) { Finish(ex.Message); }
        }
        private void Finish(string error)
        {
            _e.error=error; _e.passed=error==null && _e.rounds==2 && _e.zeroSocket && _e.hpDamage && _e.replay && _e.menuKeepsTicking && _e.returnedToMenu;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
            File.WriteAllText(_path,JsonUtility.ToJson(_e,true));
            _solo=null; Application.Quit(_e.passed?0:1);
        }
    }
}
#endif
