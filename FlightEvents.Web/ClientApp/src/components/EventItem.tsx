﻿import * as React from 'react';
import styled from 'styled-components';
import { css } from 'styled-components';
import { FlightEvent, Airport, FlightPlan } from '../Models';
import EventModal from './EventModal';
import parseJSON from 'date-fns/parseJSON';
import addHours from 'date-fns/addHours';
import formatRelative from 'date-fns/formatRelative';
import isBefore from 'date-fns/isBefore';

interface Props {
    flightEvent: FlightEvent;
    onAirportsLoaded: (airports: Airport[]) => void;
    onFlightPlansLoaded: (flightPlans: FlightPlan[]) => void;
}

interface State {
    isOpen: boolean;
}

export default class EventItem extends React.Component<Props, State> {
    constructor(props: any) {
        super(props);

        this.state = {
            isOpen: false
        };

        this.handleToggle = this.handleToggle.bind(this);
    }

    private handleToggle() {
        this.setState({
            isOpen: !this.state.isOpen
        })
    }

    public render() {
        return <ListItem>
            <CustomButton className={"btn btn-link"} endDateTime={addHours(parseJSON(this.props.flightEvent.startDateTime), 4)} onClick={this.handleToggle}>
                <EventTitle>{this.props.flightEvent.name}</EventTitle>
                <EventSubtitle>
                    ({formatRelative(parseJSON(this.props.flightEvent.startDateTime), new Date())})
                </EventSubtitle>
            </CustomButton>
            <EventModal isOpen={this.state.isOpen} toggle={this.handleToggle} flightEvent={this.props.flightEvent} onAirportLoaded={this.props.onAirportsLoaded} onFlightPlansLoaded={this.props.onFlightPlansLoaded} />
        </ListItem>
    }
}

const ListItem = styled.li`

`

const EventTitle = styled.h3`
font-size: 1.1em;
font-weight: semi-bold;
margin-bottom: 0;
`

const EventSubtitle = styled.div`
font-size: 0.9em;
text-align: right;
`

const CustomButton = styled.button<{ endDateTime: Date }>`
${props => isBefore(props.endDateTime, new Date()) && css`display: none`}
:hover, :focus {
    text-decoration: none;

    ${EventTitle} {
        text-decoration: underline;
    }
}
`