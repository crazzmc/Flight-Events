﻿import * as React from 'react';
import { Button, Modal, ModalHeader, ModalBody, Alert } from 'reactstrap';

interface State {
    modal: boolean;
}

export default class Download extends React.Component<any, State> {
    constructor(props: any) {
        super(props);

        this.state = {
            modal: false
        }

        this.toggle = this.toggle.bind(this);
    }

    toggle() {
        this.setState({
            modal: !this.state.modal
        });
    }

    public render() {
        return <>
            <Button color="primary" onClick={this.toggle} style={{ minWidth: '130px' }}>Join</Button>
            <Modal isOpen={this.state.modal} toggle={this.toggle}>
                <ModalHeader toggle={this.toggle}>Flight Events Client</ModalHeader>
                <ModalBody>
                    <p>In order to connect Microsoft Flight Simulator to this map, you have to run a client software on your computer.</p>

                    <a className="btn btn-primary" href="https://events-storage.flighttracker.tech/downloads/FlightEvents.Client.zip" target="_blank" rel="noopener noreferrer">Download MSFS Client</a>

                    <Alert color="warning">Current version of MSFS has an issue with SimConnect, creating stuttering if add-ons such as Flight Events is connected.<br />As a workaround, please update Flight Events to version 2.1.0 or later, open Settings tab, tick Slow Mode and restart Flight Events. This limits the data rate to 1Hz and seems to alleviate the issue.</Alert>

                    <hr />
                    <p>If you want to use Flight Events with FSX or P3D, you can try to use this legacy client (unsupported).</p>
                    <a className="btn btn-primary" href="https://events-storage.flighttracker.tech/downloads/FlightEvents.Client-FSX.zip" target="_blank" rel="noopener noreferrer">Download Legacy Client</a>
                    <p>This client might also ask you to download &amp; install the following prerequisites: <a href="https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.7-windows-x86-installer" target="_blank" rel="noopener noreferrer">.NET Core Windows Runtime (x86)</a></p>

                    <hr />
                    <h5>Flight Tracking</h5>
                    <p>Type in your preferred callsign and click Start Flight Tracking to show your aircraft on the map.</p>
                    <h5>Discord Connection</h5>
                    <p>The client can automatically switch you among frequency channels in participating Discord server.<br />You can enable this by connecting your Discord account, starting flight tracking and then connecting to the designated lounge Voice Channel in the Discord server.</p>
                    <h5>ATC</h5>
                    <p>The client can act as a FSD server for compatible ATC clients such as Euroscope and Aurora.<br/>You just need to click Start ATC mode and connect your favorite ATC client to localhost.</p>
                </ModalBody>
            </Modal>
        </>
    }
}