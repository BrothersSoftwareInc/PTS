using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using TiT.Unipump;
using System.Threading;

namespace TiT.PTS
{
    /// <summary>
    /// Provides control over a Pump connected to a PTS controller.
    /// </summary>
    public class Pump
    {
        private PumpStatus _status;
        private PumpChannel _channel;
        private PTS _pts;
        private Nozzle[] _nozzles;
        private byte _activeNozzleId;
        private int _channelId;
        private int _physicalAddress;

        /// <summary>
        /// Creates exemplar of Probe class.
        /// </summary>
        /// <param name="pts">Exemplar of parent PTS class.</param>
        internal Pump(PTS pts)
        {
            this.AutocloseTransaction = true;
            this._pts = pts;
            this._status = PumpStatus.OFFLINE;
            this.CommandToExecute = 0;
            this.CommandPlannedToExecute = 0;
            this.StatusReceivedCounterBeforeCommand = 0;
            this.Locked = false;

            this._nozzles = new Nozzle[PtsConfiguration.MaxNozzlesCount];
            for (int i = 0; i < PtsConfiguration.MaxNozzlesCount; i++)
            {
                _nozzles[i] = new Nozzle(this, (byte)(i + 1));
                _nozzles[i].PricePerLiter = 0;
            }
        }

        /// <summary>
        /// Gets unique identifier of a Pump.
        /// </summary>
        public int ID { get; set; }
        
        /// <summary>
        /// Gets code of command currently executed by PTS controller.
        /// </summary>
        public byte CommandNozzle { get; set; }
        
        /// <summary>
        /// Gets code of command currently executed by PTS controller.
        /// </summary>
        public int CommandDose { get; set; }

        /// <summary>
        /// Sets state of lights on pumps.
        /// </summary>
        public bool LightsState { get; set; }
        
        /// <summary>
        /// Gets code of command currently executed by PTS controller.
        /// </summary>
        public AuthorizeType AuthorizationType { get; set; }

        /// <summary>
        /// Gets or sets a code of command currently executed by PTS controller.
        /// </summary>
        public byte PendingCommand { get; set; }

        /// <summary>
        /// Gets or sets quantity of Status responses before unlocking a pump
        /// </summary>
        public short StatusReceivedCounterBeforeCommand { get; set; }

        /// <summary>
        /// Gets or sets code of command currently executed by PTS controller.
        /// </summary>
        public byte CommandToExecute { get; set; }

        /// <summary>
        /// Gets or sets time of command calling to execute.
        /// </summary>
        public DateTime CommandTime { get; set; }

        /// <summary>
        /// Gets or sets code of command currently executed by PTS controller.
        /// </summary>
        public byte CommandPlannedToExecute { get; set; }

        /// <summary>
        /// Gets or sets or sets a value, which points if a Pump is active and it is necessary to query its state.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets a value, which points whether transaction should be closed automatically or it is necessary 
        /// to close transaction manually after it is finished.
        /// </summary>
        public bool AutocloseTransaction { get; set; } 

        /// <summary>
        /// Gets an amount (in cents) for a current (in a case of an active transaction) or last fuel dispense.
        /// </summary>
        public float DispensedAmount { get; set; }

        /// <summary>
        /// Gets a volume (in 10 ml units) for a current (in a case of an active transaction) or last fuel dispense.
        /// </summary>
        public int DispensedVolume { get; set; }

        /// <summary>
        /// Gets a value indicating whether a Pump is locked.
        /// </summary>
        public bool Locked { get; set; } 

        /// <summary>
        /// Gets or sets an identifier of a current transaction.
        /// </summary>
        public int TransactionID { get; internal set; }

        /// <summary>
        /// Gets or sets a counter value for considering a pump to be offline.
        /// </summary>
        public int OfflineResponseCounter { get; set; }

        /// <summary>
        /// Gets a PTS exemplar, to which given Pump belongs to.
        /// </summary>
        public PTS PTS
        {
            get
            {
                return _pts;
            }
            set
            {
                _pts = value;
            }
        }

        /// <summary>
        /// Gets a currently taken up nozzle. 
        /// </summary>
        /// <remarks>
        /// If there is no taken up nozzle - then returns null (Nothing in Visual Basic).
        /// </remarks>
        public Nozzle ActiveNozzle
        {
            get
            {
                if (_activeNozzleId == 0)
                    return null;

                return _nozzles[_activeNozzleId - 1];
            }
        }

        /// <summary>
        /// Gets an identifier of a taken up nozzle. 
        /// </summary>
        /// <remarks>
        /// If there is no taken up nozzle - then returns 0.
        /// </remarks>
        public byte ActiveNozzleID
        {
            get
            {
                return _activeNozzleId;
            }
            internal set
            {
                if (value < 0 || value > PtsConfiguration.MaxNozzlesCount)
                {
                    _pts.OnMessageError(new MessageErrorEventArgs(new Exception("Pump[" + ID.ToString() + "] ActiveNozzleID is null or zero")));
                    return;
                }

                _activeNozzleId = value;
            }
        }

        /// <summary>
        /// Gets or sets physical address of a Pump.
        /// </summary>
        public int PhysicalAddress
        {
            get
            {
                return _physicalAddress;
            }
            set
            {
                if (value < 0 || value > PtsConfiguration.MaxPumpAddressCount)
                {
                    _pts.OnMessageError(new MessageErrorEventArgs(new Exception("Pump[" + ID.ToString() + "] PhysicalAddress is out of range, PhysicalAddress = " + value.ToString())));
                    return;
                }

                _physicalAddress = value;
            }
        }

        /// <summary>
        /// Gets or sets an identifier of a channel, to which a Pump is connected.
        /// </summary>
        /// <remarks>
        /// If a Pump is not connected to a channel then a value should be equal to zero.
        /// </remarks>
        public int ChannelID
        {
            get
            {
                return _channelId;
            }
            set
            {
                if (value < 0 || value > PtsConfiguration.MaxPumpChannelsCount)
                {
                    _pts.OnMessageError(new MessageErrorEventArgs(new Exception("Pump[" + ID.ToString() + "] ChannelID is out of range, ChannelID = " + value.ToString())));
                    return;
                }

                _channelId = value;

                if (value > 0)
                    foreach (PumpChannel pumpChannel in _pts.PumpChannels)
                        if (pumpChannel.ID == value)
                            _channel = pumpChannel;
            }
        }

        /// <summary>
        /// Gets an object PumpChannel, to which a Pump is connected.
        /// </summary>
        /// <remarks>
        /// If a Pump is not connected to a channel - returns a value null (Nothing in Visual Basic).
        /// </remarks>     
        public PumpChannel Channel
        {
            get
            {
                return _channel;
            }
            internal set
            {
                _channel = value;
                if (_channel != null) 
                    _channelId = _channel.ID;
                else 
                    _channel = null;
            }
        }

        /// <summary>
        ///  Gets an array of objects Nozzle connected to given Pump.
        /// </summary>
        public Nozzle[] Nozzles
        {
            get
            {
                return _nozzles;
            }
        }

        /// <summary>
        ///  Gets an array of prices for each Nozzle of given Pump.
        /// </summary>
        public int[] NozzlePrices
        {
            get 
            {
                int[] prices = new int[PtsConfiguration.MaxNozzlesCount];
                
                for(int i = 0; i < PtsConfiguration.MaxNozzlesCount; i++)
                    prices[i] = Nozzles[i].PricePerLiter;

                return prices;
            }
        }

        /// <summary>
        /// Gets a status of a Pump.
        /// </summary>
        public PumpStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (value < 0 || (int)value > PtsConfiguration.MaxNozzlesCount)
                {
                    _pts.OnMessageError(new MessageErrorEventArgs(new Exception("Pump[" + ID.ToString() + "] Status is out of range, Status = " + value.ToString())));
                    return;
                }

                _status = value;
            }
        }

        /// <summary>
        /// Gets quantity of times polling should be made prior to sending of command to pump
        /// </summary>
        /// <returns></returns>
        public int GetPollingQuantity()
        {
            if (_pts.ActivePumpsCount >= 1 && _pts.ActivePumpsCount <= 2)
                return 5;
            else if (_pts.ActivePumpsCount >= 3 && _pts.ActivePumpsCount <= 4)
                return 4;
            else if (_pts.ActivePumpsCount >= 5 && _pts.ActivePumpsCount <= 6)
                return 3;
            else
                return 2;
        }

        /// <summary>
        /// Method for placing in a queue next command to the performed.
        /// </summary>
        public void ExecuteCommand()
        {
            if (CommandToExecute != 0)
            {
                if (Locked == true)
                {
                    if (++StatusReceivedCounterBeforeCommand > GetPollingQuantity())
                    {
                        switch (CommandToExecute)
                        {
                            case UnipumpUtils.uUnlockRequest:
                                Unlock();
                                break;
                            case UnipumpUtils.uAutorizeRequest:
                                Authorize();
                                break;
                            case UnipumpUtils.uHaltRequest:
                                Halt();
                                break;
                            case UnipumpUtils.uSuspendRequest:
                                Suspend();
                                break;
                            case UnipumpUtils.uResumeRequest:
                                Resume();
                                break;
                            case UnipumpUtils.uCloseTransactionRequest:
                                CloseTransaction();
                                break;
                            case UnipumpUtils.uTotalsRequest:
                                GetTotals();
                                break;
                            case UnipumpUtils.uPricesGetRequest:
                                GetPrices();
                                break;
                            case UnipumpUtils.uPricesSetRequest:
                                SetPrices();
                                break;
                            case UnipumpUtils.uTagRequest:
                                GetTag();
                                break;
                            case UnipumpUtils.uLightsRequest:
                                SetLights();
                                break;
                            case UnipumpUtils.uParamSetRequest:
                                SetPrices();
                                break;
                            default:
                                GetStatus();
                                break;
                        }

                        CommandToExecute = 0;
                        StatusReceivedCounterBeforeCommand = 0;

                        if (CommandPlannedToExecute != 0 && Locked == true)
                        {
                            CommandToExecute = CommandPlannedToExecute;
                            CommandPlannedToExecute = 0;
                        }

                        return;
                    }
                }
            }
            
            GetStatus();
        }

        /// <summary>
        /// Gets status of the Pump.
        /// </summary>
        /// <remarks>
        /// Returns current status of the Pump. 
        /// </remarks>
        internal void GetStatus()
        {
            if (_pts.UseExtendedCommands == false)
                _pts.StatusRequest(ID);
            else
                _pts.ExtendedStatusRequest(ID);
        }

        /// <summary>
        /// Locks control over a Pump in a multi POS system (each POS system having a PTS controller connected).
        /// </summary>
        public void Lock()
        {
            _pts.LockRequest(ID);
        }

        /// <summary>
        /// Unlocks control over a Pump in a multi POS system (each POS system having a PTS controller connected).
        /// </summary>
        public void Unlock()
        {
            _pts.UnlockRequest(ID);
        }

        /// <summary>
        /// Sends a command on authorization to a Pump for a currently taken up nozzle and opens a transaction.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void Authorize()
        {
            if (_pts.UseExtendedCommands == false)
                _pts.AuthorizeRequest(ID, CommandNozzle, AuthorizationType, CommandDose, Nozzles[CommandNozzle - 1].PricePerLiter);
            else
                _pts.ExtendedAuthorizeRequest(ID, CommandNozzle, AuthorizationType, CommandDose, Nozzles[CommandNozzle - 1].PricePerLiter);
        }

        /// <summary>
        /// Stops dispensing of fuel through a Pump.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void Halt()
        {
            _pts.StopRequest(ID);
        }

        /// <summary>
        /// Suspends dispensing of fuel through a Pump (for those pump protocols, which support this feature).
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void Suspend()
        {
            _pts.SuspendRequest(ID);
        }

        /// <summary>
        /// Resumes dispensing of fuel through a Pump (for those pump protocols, which support this feature).
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void Resume()
        {
            _pts.ResumeRequest(ID);
        }

        /// <summary>
        /// Closes current transaction.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void CloseTransaction()
        {
            _pts.CloseTransactionRequest(ID, TransactionID);
        }

        /// <summary>
        /// Gets total counters from a Pump.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void GetTotals()
        {
            if (_pts.UseExtendedCommands == false)
                _pts.TotalsRequest(ID, CommandNozzle);
            else
                _pts.ExtendedTotalsRequest(ID, CommandNozzle);
        }

        /// <summary>
        /// Gets prices on fuel for nozzles in a Pump.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void GetPrices()
        {
            _pts.PricesGetRequest(ID);
        }

        /// <summary>
        /// Sets prices on fuel for nozzles in a Pump in accordance with prices set by
        /// properties PricePerLiter of connected objects Nozzle.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void SetPrices()
        {
            if (_pts.UseExtendedCommands == false)
                _pts.PricesSetRequest(ID, NozzlePrices);
            else
                _pts.ExtendedPricesSetRequest(ID, NozzlePrices);
        }

        /// <summary>
        /// Gets tag's ID from nozzle on a Pump.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void GetTag()
        {
            _pts.TagRequest(ID, CommandNozzle);
        }

        /// <summary>
        /// Switches on/off lights on a Pump.
        /// </summary>
        /// <remarks>
        /// Before calling this method the Pump should be locked.
        /// </remarks>
        public void SetLights()
        {
            _pts.LightsRequest(ID, LightsState);
        }

        /// <summary>
        /// EventHandler for event, which fires when nozzle is changed on a Pump.
        /// </summary>
        public event EventHandler NozzleChanged;

        /// <summary>
        /// Event fired when nozzle is changed on a Pump.
        /// </summary>
        protected internal void OnNozzleChanged()
        {
            if (NozzleChanged != null)
                NozzleChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// EventHandler for event, which fires when Status of a Pump is changed.
        /// </summary>
        public event EventHandler StatusChanged;

        /// <summary>
        /// Event fired when Status of a Pump is changed.
        /// </summary>
        protected internal void OnStatusChanged()
        {
            if (StatusChanged != null)
                StatusChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// EventHandler for event, which fires when totals are received from a Pump.
        /// </summary>
        public event EventHandler TotalsUpdated;

        /// <summary>
        /// Event fired when totals are received from a Pump.
        /// </summary>
        protected internal void OnTotalsUpdated()
        {
            if (TotalsUpdated != null)
                TotalsUpdated(this, EventArgs.Empty);
        }

        /// <summary>
        /// EventHandler for event, which fires when prices are received from a Pump.
        /// </summary>
        public event EventHandler PricesGet;

        /// <summary>
        /// Event fired when prices are received from a Pump.
        /// </summary>
        protected internal void OnPricesGet()
        {
            if (PricesGet != null)
                PricesGet(this, EventArgs.Empty);
        }

        /// <summary>
        /// EventHandler for event, which fires when transaction is finished on a Pump.
        /// </summary>
        public event EventHandler TransactionFinished;

        /// <summary>
        /// Event fired when transaction is finished on a Pump.
        /// </summary>
        protected internal void OnTransactionFinished()
        {
            if (TransactionFinished != null)
                TransactionFinished(this, EventArgs.Empty);
        }

        /// <summary>
        /// EventHandler for event, which fires when ID tag is received from a Pump.
        /// </summary>
        public event EventHandler TagGet;

        /// <summary>
        /// Event fired when ID tag is received from a Pump.
        /// </summary>
        protected internal void OnTagGet()
        {
            if (TagGet != null)
                TagGet(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Provides information about a nozzle of a Pump.
    /// </summary>
    public class Nozzle
    {
        private Pump _pump;
        private int _pricePerLiter;
        private byte _id;

        /// <summary>
        /// Creates an exemplar of Nozzle class.
        /// </summary>
        /// <param name="pump">Exemplar of parent Pump class.</param>
        /// <param name="id">Identifier of a nozzle.</param>
        internal Nozzle(Pump pump, byte id)
        {
            this._pump = pump;
            this._id = id;
        }

        /// <summary>
        /// Gets a value of totally dispensed amount of electronic total counter.
        /// </summary>
        public UInt64 TotalDispensedAmount { get; internal set; }

        /// <summary>
        /// Gets a value of totally dispensed volume of electronic total counter.
        /// </summary>
        public UInt64 TotalDispensedVolume { get; internal set; }

        /// <summary>
        /// Gets or sets a code of nozzle's ID tag.
        /// </summary>
        public string TagCode { get; internal set; }

        /// <summary>
        /// Gets an identifier of a nozzle.
        /// </summary>
        public byte ID
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// Gets an object Pump, to which a nozzle belongs to.
        /// </summary>
        public Pump Pump
        {
            get
            {
                return _pump;
            }
        }

        /// <summary>
        /// Gets or sets price of fuel per liter/gallon.
        /// </summary>
        public int PricePerLiter
        {
            get
            {
                return _pricePerLiter;
            }
            set
            {
                if (value < 0)
                {
                    Pump.PTS.OnMessageError(new MessageErrorEventArgs(new Exception("Nozzle[" + ID.ToString() + "]  PricePerLiter is out of range, PricePerLiter = " + value.ToString())));
                    return;
                }
                
                _pricePerLiter = value;
            }
        }
    }

    /// <summary>
    /// Provides information about a Pump channel of a PTS controller.
    /// </summary>
    public class PumpChannel
    {
        private Pump[] _pumps;
        private PTS _pts;

        /// <summary>
        /// Creates an exemplar of PumpChannel class.
        /// </summary>
        /// <param name="pts">Exemplar of parent PTS class.</param>
        internal PumpChannel(PTS pts)
        {
            this._pts = pts;
        }

        /// <summary>
        /// Gets or sets an identifier of a PumpChannel.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets baud rate of a PumpChannel.
        /// </summary>
        public PtsConfiguration.ChannelBaudRate BaudRate { get; set; }

        /// <summary>
        /// Gets or sets a communication protocol of a PumpChannel.
        /// </summary>
        public PtsConfiguration.ChannelProtocol Protocol { get; set; }

        /// <summary>
        /// Gets a PTS exemplar, to which given PumpChannel belongs to.
        /// </summary>
        public PTS PTS
        {
            get
            {
                return _pts;
            }
        }

        /// <summary>
        /// Gets an array of objects Pump, which belongs to given PumpChannel.
        /// </summary>
        public Pump[] Pumps
        {
            get
            {
                return _pumps;
            }
            internal set
            {
                _pumps = value;

                if (value == null) 
                    return;

                foreach (Pump pump in _pumps)
                    pump.Channel = this;
            }
        }
    }
}
