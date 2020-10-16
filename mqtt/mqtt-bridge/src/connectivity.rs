#![allow(dead_code)] // TODO remove when ready

use async_trait::async_trait;
use tracing::{debug, info};

use mqtt3::Event;

use crate::{
    client::{EventHandler, Handled},
    pump::{ConnectivityState, PumpError, PumpHandle, PumpMessage},
};

/// Handles connection and disconnection events and sends a notification when status changes
pub struct ConnectivityHandler {
    state: ConnectivityState,
    sender: PumpHandle,
}

impl ConnectivityHandler {
    pub fn new(sender: PumpHandle) -> Self {
        ConnectivityHandler {
            state: ConnectivityState::Disconnected,
            sender,
        }
    }
}

#[async_trait]
impl EventHandler for ConnectivityHandler {
    type Error = ConnectivityError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        match event {
            Event::Disconnected(reason) => {
                debug!("Received disconnected state {}", reason);
                match self.state {
                    ConnectivityState::Connected => {
                        self.state = ConnectivityState::Disconnected;

                        let msg = PumpMessage::ConnectivityUpdate(ConnectivityState::Disconnected);
                        self.sender.send(msg).await?;

                        info!("Sent disconnected state");
                    }
                    ConnectivityState::Disconnected => {
                        debug!("Already disconnected");
                    }
                }

                return Ok(Handled::Fully);
            }

            Event::NewConnection { reset_session: _ } => {
                match self.state {
                    ConnectivityState::Connected => {
                        debug!("Already connected");
                    }
                    ConnectivityState::Disconnected => {
                        self.state = ConnectivityState::Connected;

                        let msg = PumpMessage::ConnectivityUpdate(ConnectivityState::Connected);
                        self.sender.send(msg).await?;

                        info!("Sent connected state")
                    }
                }
                return Ok(Handled::Fully);
            }

            _ => {}
        }

        Ok(Handled::Skipped)
    }
}

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct ConnectivityError(#[from] PumpError);

#[cfg(test)]
mod tests {
    use mqtt3::{proto::QoS, proto::SubscribeTo, ConnectionError, Event, SubscriptionUpdateEvent};
    use tokio::sync::mpsc::error::TryRecvError;

    use crate::{
        client::Handled,
        pump::{self, ConnectivityState, PumpMessage},
    };

    use super::*;

    #[tokio::test]
    async fn sends_connected_state() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let mut ch = ConnectivityHandler::new(handle);
        let event = Event::NewConnection {
            reset_session: true,
        };
        let res = ch.handle(&event).await.unwrap();

        let msg = connectivity_receiver.try_recv().unwrap();
        assert_eq!(
            msg,
            PumpMessage::ConnectivityUpdate(ConnectivityState::Connected)
        );
        assert_eq!(ch.state, ConnectivityState::Connected);
        assert_eq!(res, Handled::Fully);
    }

    #[tokio::test]
    async fn sends_disconnected_state() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let ch = ConnectivityHandler::new(handle);

        let res_connected = ch
            .handle(&Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();
        let _msg = connectivity_receiver.try_recv().unwrap();

        let res_disconnected = ch
            .handle(&Event::Disconnected(
                ConnectionError::ServerClosedConnection,
            ))
            .await
            .unwrap();

        let msg = connectivity_receiver.try_recv().unwrap();

        assert_eq!(
            msg,
            PumpMessage::ConnectivityUpdate(ConnectivityState::Disconnected)
        );
        assert_eq!(ch.state, ConnectivityState::Disconnected);
        assert_eq!(res_connected, Handled::Fully);
        assert_eq!(res_disconnected, Handled::Fully);
    }

    #[tokio::test]
    async fn not_sends_connected_state_when_already_connected() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let ch = ConnectivityHandler::new(handle);

        let res_connected1 = ch
            .handle(&Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();

        let res_connected2 = ch
            .handle(&Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();

        let _msg = connectivity_receiver.try_recv().unwrap();
        let msg = connectivity_receiver.try_recv();
        assert_eq!(msg, Err(TryRecvError::Empty));
        assert_eq!(ch.state, ConnectivityState::Connected);
        assert_eq!(res_connected1, Handled::Fully);
        assert_eq!(res_connected2, Handled::Fully);
    }

    #[tokio::test]
    async fn not_sends_disconnected_state_when_already_disconnected() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let ch = ConnectivityHandler::new(handle);

        let res_disconnected = ch
            .handle(&Event::Disconnected(
                ConnectionError::ServerClosedConnection,
            ))
            .await
            .unwrap();

        let msg = connectivity_receiver.try_recv();
        assert_eq!(msg, Err(TryRecvError::Empty));
        assert_eq!(ch.state, ConnectivityState::Disconnected);
        assert_eq!(res_disconnected, Handled::Fully)
    }

    #[tokio::test]
    async fn not_handles_other_events() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let ch = ConnectivityHandler::new(handle);

        let event =
            Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo".into(),
                qos: QoS::AtLeastOnce,
            })]);

        let res = ch.handle(&event).await.unwrap();

        assert_eq!(res, Handled::Skipped)
    }

    #[tokio::test]
    async fn default_disconnected_state() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let ch = ConnectivityHandler::new(handle);

        assert_eq!(ch.state, ConnectivityState::Disconnected);
    }
}
