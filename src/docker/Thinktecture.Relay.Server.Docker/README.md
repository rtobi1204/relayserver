# How to Rebuild an import to K3S

Dockerfile is here
/opt/relayserver/src/docker/Thinktecture.Relay.Server.Docker/Dockerfile

To build:
cd /opt/relayserver/src
sudo docker build -f docker/Thinktecture.Relay.Server.Docker/Dockerfile -t relayserver:latest .

docker build -f docker/Thinktecture.Relay.Server.Docker/Dockerfile -t extern/relayserver:1.0.0 dockerimages.impuls-sw.com/extern/relayserver:1.0.0 .

docker push dockerimages.impuls-sw.com/extern/relayserver:1.0.0

Save and import
sudo docker save relayserver:latest -o relayserver.tar
sudo k3s ctr images import relayserver.tar

Redeploy / Update
kubectl rollout restart deployment relayserver
