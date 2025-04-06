using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Services
{
    public interface IFrontService
    {
        public void AllWindowShow();
        public void AllWindowHide();
        public void BpWindowShow();
        public void BpWindowHide();
        public void InterludeWindowShow();
        public void InterludeWindowHide();
        public void GameDataWindowShow();
        public void GameDataWindowHide();
        public void ScoreWindowShow();
        public void ScoreWindowHide();
        public void WidgetsWindowShow();
        public void WidgetsWindowHide();
    }
}
